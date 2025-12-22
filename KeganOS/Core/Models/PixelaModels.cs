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
