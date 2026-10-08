using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Refreshify.Core.Health;

/// <summary>Reads the capacities out of the XML that <c>powercfg /batteryreport /xml</c> writes.</summary>
public static class BatteryReport
{
    /// <summary>
    /// The battery in the worst condition, or null when the report has none, as on a desktop. Elements are matched by
    /// local name, so the report's namespace doesn't matter.
    /// </summary>
    public static BatteryReading? Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return null;
        }

        var batteries = document.Descendants()
            .Where(element => element.Name.LocalName == "Battery")
            .Select(element => new BatteryReading(
                Capacity(element.Elements().FirstOrDefault(child => child.Name.LocalName == "DesignCapacity")),
                Capacity(element.Elements().FirstOrDefault(child => child.Name.LocalName == "FullChargeCapacity"))))
            .Where(battery => battery.DesignCapacity > 0 && battery.FullChargeCapacity > 0)
            .ToList();

        return batteries.Count == 0
            ? null
            : batteries.MinBy(battery => (double)battery.FullChargeCapacity / battery.DesignCapacity);
    }

    private static long Capacity(XElement? element) =>
        long.TryParse(element?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
