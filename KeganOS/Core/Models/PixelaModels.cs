namespace KeganOS.Core.Models;

/// <summary>
/// Statistics from Pixe.la API
/// </summary>
public class PixelaStats
{
    public int TotalPixelsCount { get; set; }
    public double TotalQuantity { get; set; }  // Total hours
    public double MaxQuantity { get; set; }    // Max hours in a day
    public double MinQuantity { get; set; }    // Min hours in a day
    public double AvgQuantity { get; set; }    // Average hours per day
    public DateTime? MaxDate { get; set; }     // Date of record day
}

/// <summary>
/// Individual pixel (day) from Pixe.la
/// </summary>
public class PixelaPixel
{
    public DateTime Date { get; set; }
    public double Quantity { get; set; }
}

/// <summary>
/// Configuration for a Pixe.la graph
/// </summary>
public class PixelaGraphDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Type { get; set; } = "";
    public string Color { get; set; } = "";
    public string Timezone { get; set; } = "";
    public bool IsEnablePng { get; set; }
    public bool StartOnMonday { get; set; }
}
