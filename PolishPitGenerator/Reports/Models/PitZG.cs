using Models.Operations;

namespace PolishPitGenerator.Reports.Models
{
    public class PitZG
    {
        //Comments and fields are based on 2024 PIT-ZG form version

        //B. DODATKOWE INFORMACJE

        //6. Państwo uzyskania dochodu / przychodu 
        public Country Country { get; internal set; }

        //C.3. DOCHODY I PODATEK ROZLICZANE W ZEZNANIU PODATKOWYM PIT-38

        //Dochód, o którym mowa w art. 30b ust. 5a i 5b ustawy 
        public decimal C3_29 { get; internal set; }

        //Podatek zapłacony za granicą od dochodów z poz. 291) 
        public decimal C3_30 { get; internal set; }
    }
}