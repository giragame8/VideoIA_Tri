using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using SharedLogic;


namespace SharedLogic
{
    public static class CloudManager
    {
        private static readonly string WebAppUrl = "https://script.google.com/macros/s/AKfycbxpYvDVOppiqGLMfzan6bIUIyNqBa_XMv79EMrGnxSrqmSCpFPkkBAkXEs_GEttLh4L/exec";

        public static async Task EnvoyerAction(string action, string donnees)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var parametres = new Dictionary<string, string> { { "action", action }, { "data", donnees } };
                    await client.PostAsync(WebAppUrl, new FormUrlEncodedContent(parametres));
                }
            }
            catch { }
        }

        public static async Task<string> VerifierMiseAJour()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    return await client.GetStringAsync(WebAppUrl + "?type=update");
                }
            }
            catch { return ""; }
        }

        public static async Task<string> GetActivations()
        {
            try
            {
                using (HttpClient client = new HttpClient())
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
                using (HttpClient client = new HttpClient())
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
                using (HttpClient client = new HttpClient())
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
                using (HttpClient client = new HttpClient())
                {
                    string liste = await client.GetStringAsync(WebAppUrl + "?type=trials");
                    return liste.Contains(hwid);
                }
            }
            catch { return false; }
        }
    }
}