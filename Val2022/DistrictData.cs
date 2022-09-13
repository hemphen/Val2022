namespace Qaplix.Val;

public class MapData
{
    public Feature[] features { get; set; }
    public string type { get; set; }
}

public class Feature
{
    public Geometry geometry { get; set; }
    public Properties properties { get; set; }
    public string type { get; set; }
}

public class Geometry
{
    public string type { get; set; }
    public object[][][] coordinates { get; set; }
}

public class Properties
{
    public string Lkfv { get; set; }
    public string Vdnamn { get; set; }
}
