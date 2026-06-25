using System;
using System.IO;
using System.Text;
using System.Windows;

namespace VideoIA_Tri
{
    public partial class FenetreApropos : Window
    {
        private readonly string CLE_SECRETE = "ElliottVideoIAPro_SecureKey_2026";
        private string cheminUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScannerVideoIA", "user.txt");

        public FenetreApropos()
        {
            InitializeComponent();
            if (File.Exists(cheminUser))
            {
                try
                {
                    string clair = Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(cheminUser)));
                    string[] p = clair.Split(';');
                    if (p.Length >= 5)
                    {
                        TxtUserLabel.Text = $"Utilisateur : {p[0]} {p[1]}";
                        TxtEntrepriseLabel.Text = $"Entreprise : {p[2]} ({p[3]})";
                        TxtLicence.Text = "Licence : " + p[4];
                        string dec = FenetreEnregistrement.DecrypterAES(p[4], CLE_SECRETE);
                        TxtDateButoire.Text = "Expire le : " + DateTime.Parse(dec.Split('|')[3]).ToString("dd MMMM yyyy");
                    }
                }
                catch { }
            }
        }
        private void BtnFermer_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}