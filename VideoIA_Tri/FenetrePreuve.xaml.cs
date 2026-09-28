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

            ImageAgrandie.Source = ev.ImagePreuve;
            TxtTitre.Text = $"Fichier : {ev.FichierSource} | Détection : {ev.TypeEvenement} à {ev.RepereTempsLecteur}";

            if (File.Exists(ev.CheminComplet))
            {
                LecteurVideo.Source = new Uri(ev.CheminComplet);

                LecteurVideo.MediaOpened += (s, e) => {
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
            // Vérification de sécurité avant conversion pour éviter le crash
            if (!(ImageAgrandie.Source is BitmapSource sourceBitmap))
            {
                MessageBox.Show("Impossible d'enregistrer l'image : format invalide ou image manquante.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog { Filter = "Image JPEG|*.jpg", FileName = $"Capture_{_ev.TypeEvenement}" };
            if (sfd.ShowDialog() == true)
            {
                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(sourceBitmap));
                using (var stream = new FileStream(sfd.FileName, FileMode.Create))
                {
                    encoder.Save(stream);
                }
                MessageBox.Show("Image enregistrée.");
            }
        }

        private void BtnFermer_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}