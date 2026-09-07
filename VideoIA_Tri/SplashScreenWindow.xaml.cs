using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SharedLogic;

namespace VideoIA_Tri
{
    public partial class SplashScreenWindow : Window
    {
        private readonly string CLE_SECRETE = "ElliottVideoIAPro_SecureKey_2026";
        private string cheminUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScannerVideoIA", "user.txt");

        public SplashScreenWindow()
        {
            InitializeComponent();
            // ContentRendered garantit que l'UI et les animations sont affichées avant de lancer le code
            this.ContentRendered += SplashScreenWindow_ContentRendered;
        }

        private async void SplashScreenWindow_ContentRendered(object sender, EventArgs e)
        {
            await Task.Delay(500); // Laisse le temps à l'animation de commencer de façon fluide

            // --- 1. VÉRIFICATION DE MISE À JOUR (VIA L'ASSEMBLY) ---
            try
            {
                string updateData = await CloudManager.VerifierMiseAJour();
                if (!string.IsNullOrEmpty(updateData) && updateData.Contains("|"))
                {
                    string[] parts = updateData.Split('|');
                    string versionEnLigneStr = parts[0].Trim();
                    string lienTelechargement = parts[1].Trim();

                    Version versionActuelle = Assembly.GetExecutingAssembly().GetName().Version;

                    // TryParse évite un crash si Google Sheets contient des espaces ou un format invalide
                    if (Version.TryParse(versionEnLigneStr, out Version versionEnLigne))
                    {
                        if (versionEnLigne > versionActuelle)
                        {
                            MessageBoxResult rep = MessageBox.Show(
                                $"Une nouvelle version est disponible !\n\nVersion actuelle : {versionActuelle}\nNouvelle version : {versionEnLigne}\n\nVoulez-vous la télécharger maintenant ?",
                                "Mise à jour disponible",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (rep == MessageBoxResult.Yes)
                            {
                                OuvrirLienWeb(lienTelechargement);
                                Application.Current.Shutdown();
                                return;
                            }
                        }
                    }
                }
            }
            catch { /* Ignorer les erreurs réseau pour ne pas bloquer le démarrage du logiciel */ }

            // --- 2. VÉRIFICATION DE LA LICENCE ---
            bool accesAutorise = false;
            DateTime dateExpiration = DateTime.MinValue;
            string cleUtilisee = "";

            if (File.Exists(cheminUser))
            {
                try
                {
                    string clair = Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(cheminUser)));
                    string[] p = clair.Split(';');
                    if (p.Length >= 5)
                    {
                        cleUtilisee = p[4];
                        string dec = FenetreEnregistrement.DecrypterAES(cleUtilisee, CLE_SECRETE);
                        string[] cP = dec.Split('|');
                        dateExpiration = DateTime.Parse(cP[3]);

                        if (cP[0] == Environment.MachineName && DateTime.Now <= dateExpiration && cP.Length >= 4)
                        {
                            accesAutorise = true;
                        }
                    }
                }
                catch { }

                if (accesAutorise)
                {
                    bool estBannie = await CloudManager.EstCleRevoguee(cleUtilisee);
                    if (estBannie)
                    {
                        accesAutorise = false;
                        MessageBox.Show("Cette licence a été révoquée par l'administrateur. Le logiciel va se verrouiller.", "Accès Suspendu", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                if (!accesAutorise)
                {
                    File.Delete(cheminUser);
                }
                else
                {
                    int joursRestants = (int)(dateExpiration - DateTime.Now).TotalDays;
                    if (joursRestants <= 30 && joursRestants > 0)
                    {
                        MessageBox.Show($"Attention : Votre licence expire dans {joursRestants} jours.", "Alerte", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            await Task.Delay(1000); // Laisse l'animation se terminer naturellement

            if (accesAutorise)
            {
                new MainWindow().Show();
            }
            else
            {
                new FenetreEnregistrement().Show();
            }
            this.Close();
        }

        // --- MÉTHODE ULTRA-ROBUSTE POUR OUVRIR LE NAVIGATEUR ---
        private void OuvrirLienWeb(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                try
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                catch
                {
                    MessageBox.Show("Impossible d'ouvrir le navigateur automatiquement.\nVoici le lien à copier :\n\n" + url, "Lien de mise à jour", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}