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

                // On accepte les clés qui ont au moins 4 parties (Compatibilité avec l'identifiant unique anti-doublon)
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

                    // --- VÉRIFICATION DE LA RÉVOCATION EN TEMPS RÉEL ---
                    // On demande au Google Sheets si cette clé précise est bloquée
                    bool estBannie = await CloudManager.EstCleRevoguee(cleSaisie);
                    if (estBannie)
                    {
                        MessageBox.Show("Cette licence a été révoquée par l'administrateur. Activation impossible.", "Accès Refusé", MessageBoxButton.OK, MessageBoxImage.Error);
                        return; // On bloque tout de suite !
                    }

                    // Si tout est bon, on sauvegarde l'accès sur le PC
                    string dossier = Path.GetDirectoryName(cheminUser);
                    if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);

                    string infosClair = $"{TxtPrenom.Text.Trim()};{TxtNom.Text.Trim()};{parts[1]};{parts[2]};{cleSaisie}";
                    File.WriteAllText(cheminUser, Convert.ToBase64String(Encoding.UTF8.GetBytes(infosClair)));

                    // On prévient le Cloud que le client a activé son logiciel
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
    }
}