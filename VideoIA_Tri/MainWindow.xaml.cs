using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.WindowsAPICodePack.Dialogs;
using OpenCvSharp;
using Tesseract;

namespace VideoIA_Tri
{
    public partial class MainWindow : System.Windows.Window
    {
        public ObservableCollection<EvenementVideo> ListeEvenements { get; set; }
        private ICollectionView VueFiltree;
        private string dossierSelectionne = "";

        private bool estModeSombre = false;
        private bool estDispositionInversee = false;

        public MainWindow()
        {
            InitializeComponent();

            ListeEvenements = new ObservableCollection<EvenementVideo>();
            VueFiltree = CollectionViewSource.GetDefaultView(ListeEvenements);
            VueFiltree.Filter = FiltrerResultats;

            ZoneTableau.ItemsSource = VueFiltree;
        }

        private bool FiltrerResultats(object obj)
        {
            var ev = obj as EvenementVideo;
            if (ev == null) return false;

            string recherche = TxtRecherche.Text.ToLower();
            string typeFiltre = (CboFiltreType.SelectedItem as ComboBoxItem)?.Content.ToString();

            bool matchTexte = string.IsNullOrEmpty(recherche) ||
                               ev.FichierSource.ToLower().Contains(recherche) ||
                               (!string.IsNullOrEmpty(ev.Plaque) && ev.Plaque.ToLower().Contains(recherche));

            bool matchType = typeFiltre == "Tous" || ev.TypeEvenement.Contains(typeFiltre);

            return matchTexte && matchType;
        }

        private void Filtre_Changed(object sender, EventArgs e)
        {
            VueFiltree?.Refresh();
        }

        private void BtnChoisirDossier_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dossierSelectionne = dialog.FileName;
                TxtStatut.Text = $"Dossier : {Path.GetFileName(dossierSelectionne)}";
                BtnLancerAnalyse.IsEnabled = true;
            }
        }

        private async void BtnLancerAnalyse_Click(object sender, RoutedEventArgs e)
        {
            BtnLancerAnalyse.IsEnabled = false;
            ListeEvenements.Clear();
            BarreProgression.Value = 0;
            TxtPourcentage.Text = "0%";

            var fichiers = Directory.GetFiles(dossierSelectionne)
                .Where(f => new[] {
                    ".mp4", ".ts", ".avi", ".mkv",
                    ".mov", ".mpeg", ".mpg", ".m4v",
                    ".webm", ".flv", ".f4v",
                    ".3gp", ".mjpg", ".mjpeg",
                    ".trp", ".rec", ".vob",
                    ".rmvb", ".av1", ".m4s",
                    ".iva"
                }.Contains(Path.GetExtension(f).ToLower()))
                .ToArray();

            if (fichiers.Length == 0)
            {
                MessageBox.Show("Aucune vidéo trouvée.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                BtnLancerAnalyse.IsEnabled = true;
                return;
            }

            ModeAccurate modeSelectionne = ModeAccurate.Auto;
            int idx = CboPuce.SelectedIndex;
            if (idx == 1) modeSelectionne = ModeAccurate.NPU;
            else if (idx == 2) modeSelectionne = ModeAccurate.GPU;
            else if (idx == 3) modeSelectionne = ModeAccurate.CPU;

            using (var options = DetecteurPuce.ObtenirOptionsExecution(modeSelectionne, out string descriptionMatériel))
            {
                TxtStatut.Text = $"Analyse en cours [{descriptionMatériel}]...";
                await Task.Run(() => AnalyserVideosOptimise(fichiers, options));
            }

            TxtStatut.Text = "Analyse terminée !";
            BarreProgression.Value = 100;
            TxtPourcentage.Text = "100%";
            BtnLancerAnalyse.IsEnabled = true;

            NotifierFinAnalyse();
        }

        private void AnalyserVideosOptimise(string[] fichiers, SessionOptions optionsOnnx)
        {
            string cheminTessData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string cheminYolo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yolov8s.onnx");
            string cheminAnpr = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "anpr_yolov5s.onnx");

            Parallel.ForEach(fichiers, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, fichier =>
            {
                using (var moteurOCR = new TesseractEngine(cheminTessData, "eng", EngineMode.Default))
                using (var sessionYolo = new InferenceSession(cheminYolo, optionsOnnx))
                using (var sessionAnpr = File.Exists(cheminAnpr) ? new InferenceSession(cheminAnpr, optionsOnnx) : null)
                using (var video = new VideoCapture(fichier))
                {
                    Mat frame = new Mat();
                    double fps = video.Fps;
                    int totalImages = (int)video.Get(VideoCaptureProperties.FrameCount);
                    int frameIndex = 0;
                    int intervalle = Math.Max(1, (int)Math.Round(fps));
                    int dernierPourcentage = -1;

                    while (video.Read(frame) && !frame.Empty())
                    {
                        if (frameIndex % intervalle == 0)
                        {
                            var resultatDetection = DetecterObjetEtPlaqueONNX(frame, sessionYolo, sessionAnpr, moteurOCR);

                            if (resultatDetection.Objet != "Rien")
                            {
                                string heure = LireHeureSurImage(frame, moteurOCR);
                                var img = ConvertirMatPourWpf(frame);
                                double ms = video.Get(VideoCaptureProperties.PosMsec);

                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    EcranLive.Source = img;
                                    ListeEvenements.Add(new EvenementVideo
                                    {
                                        ImagePreuve = img,
                                        FichierSource = Path.GetFileName(fichier),
                                        CheminComplet = fichier,
                                        TypeEvenement = resultatDetection.Objet,
                                        Plaque = string.IsNullOrEmpty(resultatDetection.Plaque) ? "N/A" : resultatDetection.Plaque,
                                        HeureIncrustee = heure,
                                        Millisecondes = ms,
                                        RepereTempsLecteur = TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss")
                                    });
                                }));
                            }

                            if (totalImages > 0)
                            {
                                int p = (int)(((double)frameIndex / totalImages) * 100);
                                if (p != dernierPourcentage)
                                {
                                    dernierPourcentage = p;
                                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        BarreProgression.Value = p;
                                        TxtPourcentage.Text = $"{p}%";
                                    }));
                                }
                            }
                        }
                        frameIndex++;
                    }
                }
            });
        }

        private (string Objet, string Plaque) DetecterObjetEtPlaqueONNX(Mat frame, InferenceSession sessionYolo, InferenceSession sessionAnpr, TesseractEngine moteurOCR)
        {
            using (Mat resized = new Mat())
            {
                Cv2.Resize(frame, resized, new OpenCvSharp.Size(640, 640));

                var inputTensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
                for (int y = 0; y < 640; y++)
                {
                    for (int x = 0; x < 640; x++)
                    {
                        Vec3b color = resized.At<Vec3b>(y, x);
                        inputTensor[0, 0, y, x] = color.Item2 / 255.0f;
                        inputTensor[0, 1, y, x] = color.Item1 / 255.0f;
                        inputTensor[0, 2, y, x] = color.Item0 / 255.0f;
                    }
                }

                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };

                using (var results = sessionYolo.Run(inputs))
                {
                    var output = results.First().AsEnumerable<float>().ToArray();
                    int colonnes = 8400;

                    float maxC = 0; int bestIdx = -1; int bestClass = -1;

                    // Classes YOLO COCO : 0=Personne, 1=Vélo, 2=Voiture, 3=Moto, 5=Bus, 7=Camion
                    int[] classesInteret = new[] { 0, 1, 2, 3, 5, 7 };

                    for (int i = 0; i < colonnes; i++)
                    {
                        foreach (int c in classesInteret)
                        {
                            float prob = output[(4 + c) * colonnes + i];
                            if (prob > 0.60f && prob > maxC)
                            {
                                maxC = prob;
                                bestIdx = i;
                                bestClass = c;
                            }
                        }
                    }

                    if (bestIdx != -1)
                    {
                        float xc = output[0 * colonnes + bestIdx], yc = output[1 * colonnes + bestIdx];
                        float w = output[2 * colonnes + bestIdx], h = output[3 * colonnes + bestIdx];

                        float sx = (float)frame.Width / 640f;
                        float sy = (float)frame.Height / 640f;

                        int x = Math.Max(0, (int)((xc - w / 2) * sx));
                        int y = Math.Max(0, (int)((yc - h / 2) * sy));
                        int width = Math.Min(frame.Width - x, (int)(w * sx));
                        int height = Math.Min(frame.Height - y, (int)(h * sy));

                        OpenCvSharp.Rect rectObjet = new OpenCvSharp.Rect(x, y, width, height);
                        Cv2.Rectangle(frame, rectObjet, Scalar.LimeGreen, 3);

                        string nomType = "Objet";
                        string plaqueDetectee = "";

                        switch (bestClass)
                        {
                            case 0: nomType = "Personne"; break;
                            case 1: nomType = "Vélo"; break;
                            case 2: nomType = "Voiture"; break;
                            case 3: nomType = "Moto"; break;
                            case 5: nomType = "Bus"; break;
                            case 7: nomType = "Camion"; break;
                        }

                        // Détection de plaque via modèle ANPR + OCR pour les véhicules
                        if (sessionAnpr != null && (bestClass == 2 || bestClass == 3 || bestClass == 5 || bestClass == 7))
                        {
                            plaqueDetectee = DetecterPlaqueAvecANPR(frame, rectObjet, sessionAnpr, moteurOCR);
                        }

                        return (nomType, plaqueDetectee);
                    }
                }
            }

            return ("Rien", "");
        }

        private string DetecterPlaqueAvecANPR(Mat frame, OpenCvSharp.Rect rectVehicule, InferenceSession sessionAnpr, TesseractEngine engine)
        {
            try
            {
                using (Mat vehiculeMat = new Mat(frame, rectVehicule))
                using (Mat resized = new Mat())
                {
                    Cv2.Resize(vehiculeMat, resized, new OpenCvSharp.Size(640, 640));

                    var inputTensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
                    for (int y = 0; y < 640; y++)
                    {
                        for (int x = 0; x < 640; x++)
                        {
                            Vec3b color = resized.At<Vec3b>(y, x);
                            inputTensor[0, 0, y, x] = color.Item2 / 255.0f;
                            inputTensor[0, 1, y, x] = color.Item1 / 255.0f;
                            inputTensor[0, 2, y, x] = color.Item0 / 255.0f;
                        }
                    }

                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };

                    using (var results = sessionAnpr.Run(inputs))
                    {
                        var output = results.First().AsEnumerable<float>().ToArray();
                        int colonnes = 8400;

                        float maxScore = 0;
                        int bestIdx = -1;

                        for (int i = 0; i < colonnes; i++)
                        {
                            float score = output[4 * colonnes + i];
                            if (score > 0.35f && score > maxScore)
                            {
                                maxScore = score;
                                bestIdx = i;
                            }
                        }

                        if (bestIdx != -1)
                        {
                            float xc = output[0 * colonnes + bestIdx], yc = output[1 * colonnes + bestIdx];
                            float w = output[2 * colonnes + bestIdx], h = output[3 * colonnes + bestIdx];

                            float sx = (float)vehiculeMat.Width / 640f;
                            float sy = (float)vehiculeMat.Height / 640f;

                            int px = Math.Max(0, (int)((xc - w / 2) * sx));
                            int py = Math.Max(0, (int)((yc - h / 2) * sy));
                            int pw = Math.Min(vehiculeMat.Width - px, (int)(w * sx));
                            int ph = Math.Min(vehiculeMat.Height - py, (int)(h * sy));

                            OpenCvSharp.Rect rectPlaque = new OpenCvSharp.Rect(px, py, pw, ph);

                            using (Mat plaqueCrop = new Mat(vehiculeMat, rectPlaque))
                            using (Mat grayPlaque = new Mat())
                            {
                                Cv2.CvtColor(plaqueCrop, grayPlaque, ColorConversionCodes.BGR2GRAY);
                                Cv2.Resize(grayPlaque, grayPlaque, new OpenCvSharp.Size(0, 0), 2.5, 2.5, InterpolationFlags.Cubic);
                                Cv2.Threshold(grayPlaque, grayPlaque, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                                Cv2.ImEncode(".bmp", grayPlaque, out byte[] b);

                                using (var ms = new MemoryStream(b))
                                using (Bitmap bmp = new Bitmap(ms))
                                using (var p = engine.Process(bmp, PageSegMode.SingleLine))
                                {
                                    string txt = p.GetText().ToUpper();
                                    txt = Regex.Replace(txt, @"[^A-Z0-9]", ""); // Nettoyage absolu

                                    // Format SIV FR (ex: DZ735HJ)
                                    Match m1 = Regex.Match(txt, @"[A-Z]{2}[0-9]{3}[A-Z]{2}");
                                    if (m1.Success)
                                    {
                                        string raw = m1.Value;
                                        return $"{raw.Substring(0, 2)}-{raw.Substring(2, 3)}-{raw.Substring(5, 2)}";
                                    }

                                    // Correction Tesseract (O->0, I->1, B->8)
                                    string corr = NettoyerEtCorrigerPlaque(txt);
                                    Match mCorr = Regex.Match(corr, @"[A-Z]{2}[0-9]{3}[A-Z]{2}");
                                    if (mCorr.Success)
                                    {
                                        string raw = mCorr.Value;
                                        return $"{raw.Substring(0, 2)}-{raw.Substring(2, 3)}-{raw.Substring(5, 2)}";
                                    }

                                    // Sécurité anti-erreur Excel (#NOM?)
                                    string resultatBrut = txt.TrimStart('-', '=');
                                    return resultatBrut.Length >= 4 ? resultatBrut : "";
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        private string NettoyerEtCorrigerPlaque(string brute)
        {
            if (brute.Length < 7) return brute;

            char[] chars = brute.ToCharArray();

            // Si 7 caractères, on force les chiffres du milieu (positions 2, 3, 4)
            for (int i = 2; i <= 4 && i < chars.Length; i++)
            {
                if (chars[i] == 'O' || chars[i] == 'Q') chars[i] = '0';
                else if (chars[i] == 'I' || chars[i] == 'L') chars[i] = '1';
                else if (chars[i] == 'Z') chars[i] = '2';
                else if (chars[i] == 'B') chars[i] = '8';
                else if (chars[i] == 'S') chars[i] = '5';
            }

            return new string(chars);
        }

        private System.Windows.Media.Imaging.BitmapImage ConvertirMatPourWpf(Mat mat)
        {
            Cv2.ImEncode(".jpg", mat, out byte[] data);
            var img = new System.Windows.Media.Imaging.BitmapImage();
            using (var ms = new MemoryStream(data))
            {
                img.BeginInit();
                img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
            }
            return img;
        }

        private string LireHeureSurImage(Mat frame, TesseractEngine engine)
        {
            try
            {
                using (Mat cut = new Mat(frame, new OpenCvSharp.Rect(frame.Width / 2, 0, frame.Width / 2, (int)(frame.Height * 0.15))))
                using (Mat gray = new Mat())
                {
                    Cv2.CvtColor(cut, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.Resize(gray, gray, new OpenCvSharp.Size(0, 0), 2.5, 2.5, InterpolationFlags.Cubic);

                    Cv2.ImEncode(".bmp", gray, out byte[] b);

                    using (var ms = new MemoryStream(b))
                    using (Bitmap bmp = new Bitmap(ms))
                    using (var p = engine.Process(bmp, PageSegMode.Auto))
                    {
                        Match m = Regex.Match(p.GetText(), @"\d{2}:\d{2}:\d{2}");
                        return m.Success ? m.Value : "--:--:--";
                    }
                }
            }
            catch { return "--:--:--"; }
        }

        private void BtnExporterExcel_Click(object sender, RoutedEventArgs e)
        {
            if (ListeEvenements.Count == 0)
            {
                MessageBox.Show("Aucun événement à exporter.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string path = Path.Combine(dossierSelectionne, "Rapport_V1.2.csv");

            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            using (var csv = new CsvWriter(sw, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" }))
            {
                csv.WriteRecords(ListeEvenements);
            }

            MessageBox.Show("Export Excel OK : \n" + path, "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnChangerTheme_Click(object sender, RoutedEventArgs e)
        {
            estModeSombre = !estModeSombre;
            string nomDictionnaire = estModeSombre ? "ThemeSombre.xaml" : "ThemeClair.xaml";

            ResourceDictionary nouveauTheme = new ResourceDictionary()
            {
                Source = new Uri(nomDictionnaire, UriKind.Relative)
            };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(nouveauTheme);

            BtnChangerTheme.Content = estModeSombre ? "Mode Clair" : "Mode Sombre";
        }

        private void BtnChangerDisposition_Click(object sender, RoutedEventArgs e)
        {
            estDispositionInversee = !estDispositionInversee;

            if (estDispositionInversee)
            {
                Grid.SetColumn(ZoneVideo, 1);
                ZoneVideo.Margin = new Thickness(10, 0, 0, 0);

                Grid.SetColumn(ZoneTableau, 0);

                ColGauche.Width = new GridLength(60, GridUnitType.Star);
                ColDroite.Width = new GridLength(40, GridUnitType.Star);
            }
            else
            {
                Grid.SetColumn(ZoneVideo, 0);
                ZoneVideo.Margin = new Thickness(0, 0, 10, 0);

                Grid.SetColumn(ZoneTableau, 1);

                ColGauche.Width = new GridLength(40, GridUnitType.Star);
                ColDroite.Width = new GridLength(60, GridUnitType.Star);
            }
        }

        private void BtnApropos_Click(object sender, RoutedEventArgs e)
        {
            new FenetreApropos().ShowDialog();
        }

        private void ImagePreuve_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var img = sender as System.Windows.Controls.Image;
            var ev = img?.DataContext as EvenementVideo;

            if (ev != null)
            {
                new FenetrePreuve(ev).ShowDialog();
            }
        }

        private void NotifierFinAnalyse()
        {
            new ToastContentBuilder()
                .AddText("Analyse terminée")
                .AddText("Toutes les vidéos ont été traitées.")
                .Show();
        }
    }
}