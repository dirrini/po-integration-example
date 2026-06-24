namespace Nexus.Orders.Application.Models;

public class SalesOrder
{
    public string ProjectExternalCode { get; set; } = "SAP-PROJ-001";
    public string PoNumber { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Status { get; set; } = "released";
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialDescription { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string DeliveryDate { get; set; } = string.Empty;
}
