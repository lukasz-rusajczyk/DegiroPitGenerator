using System.Collections.Generic;

namespace PolishPitGenerator.Reports.Models
{
    public class Pit38Report
    {
        //Comments and fields are based on 2024 PIT-38 form version

        //DOCHODY / STRATY – ART. 30B UST. 1 USTAWY

        //Inne przychody - Przychód
        public decimal C22 { get; set; } 

        //Inne przychody - Koszty uzyskania przychodów
        public decimal C23 { get; set; } 

        //G. PODATEK DO ZAPŁATY / NADPŁATA

        //Zryczałtowany podatek obliczony od przychodów (dochodów),
        //o których mowa w art. 30a ust. 1 pkt 1–5 ustawy, 
        //uzyskanych poza granicami Rzeczypospolitej Polskiej
        public decimal G45 { get; set; } 

        //Podatek zapłacony za granicą, 
        //o którym mowa w art. 30a ust. 9 ustawy (przeliczony na złote)
        public decimal G46 { get; set; }

        //Różnica między zryczałtowanym podatkiem a podatkiem zapłaconym za granicą (po zaokrągleniu do pełnych złotych)
        public decimal G47 { get; set; }

        
        public IEnumerable<PitZG> PitZGs { get; set; }
    }
}