namespace Qaplix.Val;

public class RostData
{
    public string valtillfalle { get; set; }
    public string rakningstillfalle { get; set; }
    public string valtyp { get; set; }
    public bool test { get; set; }
    public DateTime? senasteUppdateringstid { get; set; }
    public int antalUppdateringar { get; set; }
    public int antalValdistriktRaknade { get; set; }
    public int antalValdistriktSomSkaRaknas { get; set; }
    public Valdistrikt[] valdistrikt { get; set; }
}

public class Valdistrikt
{
    public string namn { get; set; }
    public string valdistriktstyp { get; set; }
    public string rapporteringsTid { get; set; }
    public int totaltAntalRoster { get; set; }
    public int? antalRostberattigade { get; set; }
    public float? valdeltagandeVallokal { get; set; }
    public string valdistriktskod { get; set; }
    public string kommunkod { get; set; }
    public string lankod { get; set; }
    public string valomradeskod { get; set; }
    public string kretskod { get; set; }
    public Rostfordelning rostfordelning { get; set; }
}

public class Rostfordelning
{
    public Rosterpaverkamandat rosterPaverkaMandat { get; set; }
    public Rosterejpaverkamandat rosterEjPaverkaMandat { get; set; }
}

public class Rosterpaverkamandat
{
    public int antalRoster { get; set; }
    public Partiroster[] partiRoster { get; set; }
    public Rosterovrigapartier rosterOvrigaPartier { get; set; }
}

public class Rosterovrigapartier
{
    public int antalRoster { get; set; }
    public float andelRoster { get; set; }
}

public class Partiroster
{
    public string partibeteckning { get; set; }
    public string partiforkortning { get; set; }
    public string partikod { get; set; }
    public string fargkod { get; set; }
    public int ordningsnummer { get; set; }
    public int antalRoster { get; set; }
    public float andelRoster { get; set; }
}

public class Rosterejpaverkamandat
{
    public int antalRoster { get; set; }
    public float andelRosterAvTotaltAntalRoster { get; set; }
    public Rosterejanmaltdeltagande rosterEjAnmaltDeltagande { get; set; }
    public Blankaroster blankaRoster { get; set; }
    public Ovrigaogiltiga ovrigaOgiltiga { get; set; }
}

public class Rosterejanmaltdeltagande
{
    public int antalRoster { get; set; }
    public float andelRosterAvTotaltAntalRoster { get; set; }
}

public class Blankaroster
{
    public int antalRoster { get; set; }
    public float andelRosterAvTotaltAntalRoster { get; set; }
}

public class Ovrigaogiltiga
{
    public int antalRoster { get; set; }
    public float andelRosterAvTotaltAntalRoster { get; set; }
}



public class MandatData
{
    public string valtillfalle { get; set; }
    public string rakningstillfalle { get; set; }
    public string valtyp { get; set; }
    public bool test { get; set; }
    public DateTime senasteUppdateringstid { get; set; }
    public int antalUppdateringar { get; set; }
    public Valomrade valomrade { get; set; }
}

public class Valomrade
{
    public string namn { get; set; }
    public string kod { get; set; }
    public DateTime rapporteringsTid { get; set; }
    public int antalValdistriktRaknade { get; set; }
    public int antalValdistriktSomSkaRaknas { get; set; }
    public int totaltAntalRoster { get; set; }
    public int antalRostberattigade { get; set; }
    public float valdeltagande { get; set; }
    public int antalRostberattigadeIRaknadeValdistrikt { get; set; }
    public float valomradessparrProcent { get; set; }
    public string meddelandetext { get; set; }
    public Rostfordelning rostfordelning { get; set; }
    public Mandatfordelning mandatfordelning { get; set; }
}

public class Mandatfordelning
{
    public Partilista[] partiLista { get; set; }
}

public class Partilista
{
    public string partibeteckning { get; set; }
    public string partikod { get; set; }
    public string partiforkortning { get; set; }
    public int antalMandat { get; set; }
    public int antalFastaMandat { get; set; }
    public int antalUtjamningsmandat { get; set; }
}
