using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace VideoIA_Tri
{
    public partial class FenetrePreuve : Window
    {
        private EvenementVideo _ev;

        public FenetrePreuve(EvenementVideo ev)
        {
            InitializeComponent();
            _ev = ev;

            // 1. On affiche l'image et les textes
            ImageAgrandie.Source = ev.ImagePreuve;
            TxtTitre.Text = $"Fichier : {ev.FichierSource} | Détection : {ev.TypeEvenement} à {ev.RepereTempsLecteur}";

            // 2. On prépare le lecteur vidéo
            if (File.Exists(ev.CheminComplet))
            {
                LecteurVideo.Source = new Uri(ev.CheminComplet);

                // On s'abonne à l'événement de chargement pour aller au bon moment
                LecteurVideo.MediaOpened += (s, e) => {
                    // Aller 3 secondes avant (ou au début si < 3s)
                    double debutAction = Math.Max(0, ev.Millisecondes - 3000);
                    LecteurVideo.Position = TimeSpan.FromMilliseconds(debutAction);
                    LecteurVideo.Play();
                };
            }
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (LecteurVideo.CanPause) LecteurVideo.Pause();
            else LecteurVideo.Play();
        }

        private void BtnReplay_Click(object sender, RoutedEventArgs e)
        {
            double debutAction = Math.Max(0, _ev.Millisecondes - 3000);
            LecteurVideo.Position = TimeSpan.FromMilliseconds(debutAction);
            LecteurVideo.Play();
        }

        private void BtnSauvegarder_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { Filter = "Image JPEG|*.jpg", FileName = $"Capture_{_ev.TypeEvenement}" };
            if (sfd.ShowDialog() == true)
            {
                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)ImageAgrandie.Source));
                using (var stream = new FileStream(sfd.FileName, FileMode.Create)) { encoder.Save(stream); }
                MessageBox.Show("Image enregistrée.");
            }
        }

        private void BtnFermer_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}