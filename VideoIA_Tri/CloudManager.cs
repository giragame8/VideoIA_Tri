using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SharedLogic
{
    public enum StatutSupportVersion
    {
        A_Jour,
        MiseAJourDisponible,  // 1 à 4 révisions / versions de retard
        BientotObsolete,       // 5 à 9 révisions / versions de retard
        NonSupporte            // >= 10 révisions / versions de retard
    }

    public static class CloudManager
    {
        private static readonly string WebAppUrl = "https://script.google.com/macros/s/AKfycbxpYvDVOppiqGLMfzan6bIUIyNqBa_XMv79EMrGnxSrqmSCpFPkkBAkXEs_GEttLh4L/exec";

        private static HttpClient ObtenirClientHttp()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };
            return new HttpClient(handler);
        }

        public static async Task<string> VerifierMiseAJour()
        {
            try
            {
                using (HttpClient client = ObtenirClientHttp())
                {
                    return await client.GetStringAsync(WebAppUrl + "?type=update");
                }
            }
            catch { return ""; }
        }

        public static StatutSupportVersion EvaluerStatutVersion(Version versionActuelle, Version versionServeur, out int ecart)
        {
            ecart = 0;
            if (versionServeur <= versionActuelle)
                return StatutSupportVersion.A_Jour;

            // 1. Changement de Version Majeure (ex: 1.3.9.0 -> 2.0.0.0)
            if (versionServeur.Major > versionActuelle.Major)
            {
                ecart = (versionServeur.Major - versionActuelle.Major) * 10;
            }
            // 2. Changement de Version Mineure (ex: 1.3.9.0 -> 1.4.0.0)
            else if (versionServeur.Minor > versionActuelle.Minor)
            {
                // Un saut de version mineure (1.3 -> 1.4) compte pour au moins 1 version d'écart
                int diffMinor = versionServeur.Minor - versionActuelle.Minor;

                // Si c'est juste la mineure suivante (ex: 1.3 -> 1.4), c'est une simple mise à jour (ecart = 1)
                ecart = diffMinor;
            }
            // 3. Changement de Build (ex: 1.3.1.0 -> 1.3.2.0)
            else if (versionServeur.Build > versionActuelle.Build)
            {
                ecart = versionServeur.Build - versionActuelle.Build;
            }
            // 4. Correctif d'entre-deux / Revision (ex: 1.3.1.0 -> 1.3.1.5)
            else
            {
                ecart = 1;
            }

            if (ecart <= 0) ecart = 1;

            if (ecart >= 10)
                return StatutSupportVersion.NonSupporte;
            if (ecart >= 5)
                return StatutSupportVersion.BientotObsolete;

            return StatutSupportVersion.MiseAJourDisponible;
        }

        public static async Task EnvoyerAction(string action, string donnees)
        {
            try
            {
                using (HttpClient client = ObtenirClientHttp())
                {
                    var parametres = new Dictionary<string, string>
                    {
                        { "action", action },
                        { "data", donnees }
                    };
                    await client.PostAsync(WebAppUrl, new FormUrlEncodedContent(parametres));
                }
            }
            catch { }
        }

        public static async Task<string> GetActivations()
        {
            try
            {
                using (HttpClient client = ObtenirClientHttp())
                {
                    return await client.GetStringAsync(WebAppUrl + "?type=activations");
                }
            }
            catch { return ""; }
        }

        public static async Task<List<string>> GetEmailsAutorises()
        {
            try
            {
                using (HttpClient client = ObtenirClientHttp())
                {
                    string response = await client.GetStringAsync(WebAppUrl + "?type=admins");
                    if (string.IsNullOrWhiteSpace(response)) return new List<string>();
                    return new List<string>(response.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                }
            }
            catch { return new List<string>(); }
        }

        public static async Task<bool> EstCleRevoguee(string cle)
        {
            try
            {
                using (HttpClient client = ObtenirClientHttp())
                {
                    string blacklist = await client.GetStringAsync(WebAppUrl);
                    return blacklist.Contains(cle);
                }
            }
            catch { return false; }
        }

        public static async Task<bool> ADejaFaitEssai(string hwid)
        {
            try
            {
                using (HttpClient client = ObtenirClientHttp())
                {
                    string liste = await client.GetStringAsync(WebAppUrl + "?type=trials");
                    return liste.Contains(hwid);
                }
            }
            catch { return false; }
        }
    }
}