using CsvHelper.Configuration.Attributes;
using System.Windows.Media;

namespace VideoIA_Tri
{
    public class EvenementVideo
    {
        [Ignore] public ImageSource ImagePreuve { get; set; }
        public string FichierSource { get; set; }
        public string TypeEvenement { get; set; }
        public string Plaque { get; set; } // Nouveau champ pour le numéro de plaque
        public string HeureIncrustee { get; set; }
        public string RepereTempsLecteur { get; set; }

        [Ignore] public string CheminComplet { get; set; }
        [Ignore] public double Millisecondes { get; set; }
    }
}