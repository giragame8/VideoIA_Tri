using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using SharedLogic;

namespace VideoIA_Tri
{
    public partial class FenetreEnregistrement : Window
    {
        private readonly string CLE_SECRETE = "ElliottVideoIAPro_SecureKey_2026";
        private string cheminUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScannerVideoIA", "user.txt");

        public FenetreEnregistrement()
        {
            InitializeComponent();
            TxtCodeMachine.Text = Environment.MachineName;
        }

        // --- 1. ACTIVATION CLASSIQUE ---
        private async void BtnValider_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPrenom.Text) || string.IsNullOrWhiteSpace(TxtNom.Text) || string.IsNullOrWhiteSpace(TxtLicence.Text))
            {
                MessageBox.Show("Veuillez remplir tous les champs.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string cleSaisie = TxtLicence.Text.Trim();
                string decrypte = DecrypterAES(cleSaisie, CLE_SECRETE);
                string[] parts = decrypte.Split('|');

                if (parts.Length >= 4)
                {
                    if (parts[0] != Environment.MachineName)
                    {
                        MessageBox.Show("Cette licence n'est pas valide pour cet ordinateur !", "Fraude Détectée", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (DateTime.Now > DateTime.Parse(parts[3]))
                    {
                        MessageBox.Show("Cette licence a expiré.", "Expiration", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    bool estBannie = await CloudManager.EstCleRevoguee(cleSaisie);
                    if (estBannie)
                    {
                        MessageBox.Show("Cette licence a été révoquée par l'administrateur. Activation impossible.", "Accès Refusé", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string dossier = Path.GetDirectoryName(cheminUser);
                    if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);

                    string infosClair = $"{TxtPrenom.Text.Trim()};{TxtNom.Text.Trim()};{parts[1]};{parts[2]};{cleSaisie}";
                    File.WriteAllText(cheminUser, Convert.ToBase64String(Encoding.UTF8.GetBytes(infosClair)));

                    await CloudManager.EnvoyerAction("ACT", $"{TxtPrenom.Text}|{TxtNom.Text}|{Environment.MachineName}|{cleSaisie}");

                    MessageBox.Show("Activation réussie ! Bienvenue.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                    new MainWindow().Show();
                    this.Close();
                }
                else
                {
                    throw new Exception("Format de clé invalide.");
                }
            }
            catch
            {
                MessageBox.Show("Clé de licence invalide ou corrompue.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- 2. ACTIVATION DE L'ESSAI GRATUIT (NOUVEAU) ---
        private async void BtnEssai_Click(object sender, RoutedEventArgs e)
        {
            BtnEssai.IsEnabled = false;
            BtnEssai.Content = "Vérification...";

            try
            {
                // 1. Vérifier si le PC a déjà profité de l'essai
                bool dejaFait = await CloudManager.ADejaFaitEssai(Environment.MachineName);
                if (dejaFait)
                {
                    MessageBox.Show("Cet ordinateur a déjà bénéficié de la période d'essai.", "Refusé", MessageBoxButton.OK, MessageBoxImage.Error);
                    BtnEssai.IsEnabled = true;
                    BtnEssai.Content = "Démarrer l'essai gratuit (15 jours)";
                    return;
                }

                // 2. Générer une clé d'essai unique localement
                DateTime exp = DateTime.Now.AddDays(15);
                string uid = Guid.NewGuid().ToString().Substring(0, 8);
                string payload = $"{Environment.MachineName}|Essai Gratuit|Essai|{exp:yyyy-MM-dd}|{uid}";
                string cleEssai = ChiffrerAES(payload, CLE_SECRETE);

                // 3. Sauvegarder la clé localement
                string dossier = Path.GetDirectoryName(cheminUser);
                if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);

                // On met "Utilisateur" "Essai" à la place du Nom/Prénom
                string infosClair = $"Utilisateur;Essai;Essai Gratuit;Essai;{cleEssai}";
                File.WriteAllText(cheminUser, Convert.ToBase64String(Encoding.UTF8.GetBytes(infosClair)));

                // 4. Inscrire le PC dans Google Sheets pour l'empêcher de recommencer
                await CloudManager.EnvoyerAction("TRIAL", $"{Environment.MachineName}|{cleEssai}|{exp:yyyy-MM-dd}");

                MessageBox.Show($"Essai de 15 jours activé avec succès !\nValable jusqu'au {exp:dd/MM/yyyy}.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                new MainWindow().Show();
                this.Close();
            }
            catch
            {
                MessageBox.Show("Impossible de se connecter au serveur pour valider l'essai. Vérifiez votre connexion internet.", "Erreur réseau", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnEssai.IsEnabled = true;
                BtnEssai.Content = "Démarrer l'essai gratuit (15 jours)";
            }
        }

        // --- 3. FONCTIONS DE CRYPTAGE ---
        public static string DecrypterAES(string texteBase64, string mdp)
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(texteBase64);
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(mdp);
                aes.IV = iv;
                using (var ms = new MemoryStream(buffer))
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs)) { return sr.ReadToEnd(); }
            }
        }

        private string ChiffrerAES(string texte, string mdp)
        {
            byte[] iv = new byte[16];
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(mdp);
                aes.IV = iv;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(texte);
                        }
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
    }
}