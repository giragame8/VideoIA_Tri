using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms; // Nécessaire pour NotifyIcon
using System.Windows.Media.Animation;
using SharedLogic;

namespace VideoIA_Tri
{
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
        {
            InitializeComponent();
            _ = InitialiserLogicielAsync();
        }

        private async Task InitialiserLogicielAsync()
        {
            try
            {
                AnimerBarreFluide(0, 30, 400);
                MettreAJourTexte("Chargement des composants...");
                await Task.Delay(450);

                AnimerBarreFluide(30, 60, 400);
                MettreAJourTexte("Vérification des versions réseau...");
                await Task.Delay(450);

                Version versionActuelle = Assembly.GetExecutingAssembly().GetName().Version;
                string reponseMAJ = await CloudManager.VerifierMiseAJour();

                AnimerBarreFluide(60, 85, 300);
                MettreAJourTexte("Analyse de la licence...");

                if (!string.IsNullOrWhiteSpace(reponseMAJ) && reponseMAJ.Contains("|"))
                {
                    var parts = reponseMAJ.Split('|');
                    string strVersionServeur = parts[0].Trim();

                    // Normalisation sur 4 chiffres si nécessaire
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
                                // 1 à 4 révisions de retard : Information simple
                                AfficherNotificationWindows(
                                    "Mise à jour disponible ℹ️",
                                    $"Version {versionServeur} disponible (Installée: {versionActuelle}). Cliquez pour télécharger.",
                                    ToolTipIcon.Info,
                                    urlTelechargement
                                );
                                break;

                            case StatutSupportVersion.BientotObsolete:
                                // 5 à 9 révisions de retard : Avertissement
                                AfficherNotificationWindows(
                                    "Version bientôt obsolète ⚠️",
                                    $"Votre version ({versionActuelle}) a {ecart} révisions de retard sur la {versionServeur}. Cliquez pour mettre à jour.",
                                    ToolTipIcon.Warning,
                                    urlTelechargement
                                );
                                break;

                            case StatutSupportVersion.NonSupporte:
                                // 10 révisions et plus : Alerte critique
                                AfficherNotificationWindows(
                                    "Version non supportée ⛔",
                                    $"Version {versionActuelle} obsolète (Officielle: {versionServeur}). Support désactivé. Cliquez pour télécharger.",
                                    ToolTipIcon.Error,
                                    urlTelechargement
                                );
                                break;
                        }
                    }
                }

                AnimerBarreFluide(85, 100, 300);
                MettreAJourTexte("Lancement de l'application...");
                await Task.Delay(350);

                MainWindow main = new MainWindow();
                main.Show();
                this.Close();
            }
            catch
            {
                MainWindow main = new MainWindow();
                main.Show();
                this.Close();
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

                    // Redirection au clic sur le toast
                    notifyIcon.BalloonTipClicked += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            Process.Start(url);
                        }
                        notifyIcon.Dispose();
                    };

                    notifyIcon.ShowBalloonTip(6000);
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