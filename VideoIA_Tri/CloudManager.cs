using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SharedLogic
{
    public static class CloudManager
    {
        private static readonly string WebAppUrl = "https://script.google.com/macros/s/AKfycbwgYyabBkwO81pYBauT82ALVLJ0hJdcy4iu1K0z6Skjqwf1_C2CIckf1GJhnCzm7yOm/exec";

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

        // NOUVEAU : Vérifie silencieusement si la clé est sur liste noire
        public static async Task<bool> EstCleRevoguee(string cle)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5); // Ne bloque pas le PC si pas d'internet
                    string blacklist = await client.GetStringAsync(WebAppUrl);
                    return blacklist.Contains(cle);
                }
            }
            catch
            {
                return false; // Si le client n'a pas internet, on le laisse passer (pour l'instant)
            }
        }
    }
}