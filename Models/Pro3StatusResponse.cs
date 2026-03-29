namespace HeatHarmony.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public sealed class Pro3StatusResponse
    {

        public int id { get; set; }
        public string? source { get; set; }
        public bool output { get; set; }
        public Pro3Temperature? temperature { get; set; }

    }

    public sealed class Pro3Temperature 
    {
        public double tC { get; set; }
        public double tF { get; set; }
    }
}
