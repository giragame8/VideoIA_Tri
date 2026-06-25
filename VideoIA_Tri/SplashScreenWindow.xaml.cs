using System;
using System.IO;
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
            this.Loaded += SplashScreenWindow_Loaded;
        }

        private async void SplashScreenWindow_Loaded(object sender, RoutedEventArgs e)
        {
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

                        // On vérifie que la clé correspond au PC et qu'elle contient bien les bonnes infos (>= 4)
                        if (cP[0] == Environment.MachineName && DateTime.Now <= dateExpiration && cP.Length >= 4)
                        {
                            accesAutorise = true;
                        }
                    }
                }
                catch { }

                // --- VÉRIFICATION CLOUD (LA RÉVOCATION) ---
                if (accesAutorise)
                {
                    // Demande silencieuse au serveur pour voir si la clé est sur la liste noire
                    bool estBannie = await CloudManager.EstCleRevoguee(cleUtilisee);
                    if (estBannie)
                    {
                        accesAutorise = false;
                        MessageBox.Show("Cette licence a été révoquée par l'administrateur. Le logiciel va se verrouiller.", "Accès Suspendu", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // Si accès refusé (banni, expiré ou hacké) -> On détruit le fichier local
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

            // On attend 2 secondes pour laisser l'animation de chargement s'afficher
            await Task.Delay(2000);

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
    }
}