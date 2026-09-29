using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms; // Nécessaire pour NotifyIcon
using System.Windows.Media.Animation;
using SharedLogic;

namespace VideoIA_Tri
{
    public partial class SplashScreenWindow : Window
    {
        private readonly string cheminUser = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScannerVideoIA",
            "user.txt"
        );

        public SplashScreenWindow()
        {
            InitializeComponent();
            _ = InitialiserLogicielAsync();
        }

        private async Task InitialiserLogicielAsync()
        {
            try
            {
                AnimerBarreFluide(0, 25, 300);
                MettreAJourTexte("Chargement des composants...");
                await Task.Delay(350);

                AnimerBarreFluide(25, 50, 300);
                MettreAJourTexte("Vérification des mises à jour...");
                await Task.Delay(350);

                // --- 1. VÉRIFICATION DE LA VERSION ---
                Version versionActuelle = Assembly.GetExecutingAssembly().GetName().Version;
                string reponseMAJ = await CloudManager.VerifierMiseAJour();

                if (!string.IsNullOrWhiteSpace(reponseMAJ) && reponseMAJ.Contains("|"))
                {
                    var parts = reponseMAJ.Split('|');
                    string strVersionServeur = parts[0].Trim();

                    if (!strVersionServeur.Contains(".")) strVersionServeur += ".0.0.0";
                    else if (strVersionServeur.Split('.').Length == 2) strVersionServeur += ".0.0";
                    else if (strVersionServeur.Split('.').Length == 3) strVersionServeur += ".0";

                    if (Version.TryParse(strVersionServeur, out Version versionServeur))
                    {
                        string urlTelechargement = parts.Length > 1 ? parts[1] : "";
                        var statut = CloudManager.EvaluerStatutVersion(versionActuelle, versionServeur, out int ecart);

                        switch (statut)
                        {
                            case StatutSupportVersion.MiseAJourDisponible:
                                AfficherNotificationWindows(
                                    "Mise à jour disponible ℹ️",
                                    $"Version {versionServeur} disponible (Installée: {versionActuelle}). Cliquez pour télécharger.",
                                    ToolTipIcon.Info,
                                    urlTelechargement
                                );
                                break;

                            case StatutSupportVersion.BientotObsolete:
                                AfficherNotificationWindows(
                                    "Version bientôt obsolète ⚠️",
                                    $"Votre version ({versionActuelle}) a {ecart} révision(s) de retard. Cliquez pour mettre à jour.",
                                    ToolTipIcon.Warning,
                                    urlTelechargement
                                );
                                break;

                            case StatutSupportVersion.NonSupporte:
                                AfficherNotificationWindows(
                                    "Version non supportée ⛔",
                                    $"Version {versionActuelle} obsolète (Officielle: {versionServeur}). Cliquez pour mettre à jour.",
                                    ToolTipIcon.Error,
                                    urlTelechargement
                                );
                                break;
                        }
                    }
                }

                AnimerBarreFluide(50, 80, 300);
                MettreAJourTexte("Vérification de la licence...");
                await Task.Delay(350);

                // --- 2. VÉRIFICATION STRICTE DE LA LICENCE ---
                bool licenceValide = await VerifierLicenceAsync();

                AnimerBarreFluide(80, 100, 200);
                MettreAJourTexte("Lancement de l'application...");
                await Task.Delay(250);

                if (licenceValide)
                {
                    MainWindow main = new MainWindow();
                    main.Show();
                    this.Close();
                }
                else
                {
                    // Fichier absent ou licence invalide/expirée -> Redirection vers l'enregistrement
                    FenetreEnregistrement enregistrement = new FenetreEnregistrement();
                    enregistrement.Show();
                    this.Close();
                }
            }
            catch
            {
                // En cas d'erreur critique, on force la demande de licence
                FenetreEnregistrement enregistrement = new FenetreEnregistrement();
                enregistrement.Show();
                this.Close();
            }
        }

        private async Task<bool> VerifierLicenceAsync()
        {
            // Fichier AppData supprimé ou introuvable
            if (!File.Exists(cheminUser))
            {
                AfficherNotificationWindows(
                    "Licence requise 🔑",
                    "Aucune licence active détectée. Veuillez saisir votre clé ou démarrer un essai gratuit.",
                    ToolTipIcon.Warning,
                    ""
                );
                return false;
            }

            try
            {
                string contenuBase64 = File.ReadAllText(cheminUser);
                string contenuClair = Encoding.UTF8.GetString(Convert.FromBase64String(contenuBase64));
                string[] parts = contenuClair.Split(';');

                if (parts.Length < 5)
                {
                    AfficherNotificationWindows(
                        "Licence corrompue ⚠️",
                        "Fichier de licence invalide. Veuillez vous ré-enregistrer.",
                        ToolTipIcon.Error,
                        ""
                    );
                    return false;
                }

                string prenom = parts[0];
                string cleLicence = parts[4];

                // Révocation Google Sheets (Blacklist)
                bool estRevoguee = await CloudManager.EstCleRevoguee(cleLicence);
                if (estRevoguee)
                {
                    File.Delete(cheminUser);
                    AfficherNotificationWindows(
                        "Licence révoquée ⛔",
                        "Votre licence a été désactivée par l'administrateur.",
                        ToolTipIcon.Error,
                        ""
                    );
                    return false;
                }

                // Décodage de la date d'expiration dans la clé AES
                string decrypte = FenetreEnregistrement.DecrypterAES(cleLicence, "ElliottVideoIAPro_SecureKey_2026");
                string[] dataKey = decrypte.Split('|');

                if (dataKey.Length >= 4 && DateTime.TryParse(dataKey[3], out DateTime dateExp))
                {
                    if (DateTime.Now > dateExp)
                    {
                        AfficherNotificationWindows(
                            "Licence expirée ⌛",
                            $"Votre licence a expiré le {dateExp:dd/MM/yyyy}. Veuillez entrer une nouvelle clé.",
                            ToolTipIcon.Warning,
                            ""
                        );
                        return false;
                    }

                    int joursRestants = (dateExp - DateTime.Now).Days;

                    if (joursRestants <= 7)
                    {
                        AfficherNotificationWindows(
                            "Expiration imminente ⚠️",
                            $"Bienvenue {prenom} ! Attention, votre licence expire dans {joursRestants} jour(s).",
                            ToolTipIcon.Warning,
                            ""
                        );
                    }
                    else
                    {
                        AfficherNotificationWindows(
                            "Bienvenue 👋",
                            $"Licence active. Content de vous revoir {prenom} !",
                            ToolTipIcon.Info,
                            ""
                        );
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void AfficherNotificationWindows(string titre, string message, ToolTipIcon icone, string url)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    NotifyIcon notifyIcon = new NotifyIcon
                    {
                        Icon = System.Drawing.SystemIcons.Information,
                        Visible = true,
                        BalloonTipTitle = titre,
                        BalloonTipText = message,
                        BalloonTipIcon = icone
                    };

                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        notifyIcon.BalloonTipClicked += (s, e) =>
                        {
                            try { Process.Start(url); } catch { }
                            notifyIcon.Dispose();
                        };
                    }

                    notifyIcon.ShowBalloonTip(5000);
                }
                catch { }
            });
        }

        private void AnimerBarreFluide(double valeurDepart, double valeurArrivee, int dureeMs)
        {
            Dispatcher.Invoke(() =>
            {
                if (BarreSplash != null)
                {
                    DoubleAnimation animation = new DoubleAnimation
                    {
                        From = valeurDepart,
                        To = valeurArrivee,
                        Duration = new Duration(TimeSpan.FromMilliseconds(dureeMs)),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    BarreSplash.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
                }
            });
        }

        private void MettreAJourTexte(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (TxtStatus != null) TxtStatus.Text = message;
            });
        }
    }
}