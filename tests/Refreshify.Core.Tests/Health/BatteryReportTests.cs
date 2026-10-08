using Refreshify.Core.Health;

namespace Refreshify.Core.Tests.Health;

public class BatteryReportTests
{
    private const string OneBattery = """
        <?xml version="1.0" encoding="utf-8"?>
        <BatteryReport xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <ReportInformation>
            <ScanTime>2026-01-02 09:00:00</ScanTime>
          </ReportInformation>
          <Batteries>
            <Battery>
              <Id>1</Id>
              <Chemistry>LION</Chemistry>
              <DesignCapacity>57000</DesignCapacity>
              <FullChargeCapacity>45000</FullChargeCapacity>
              <CycleCount>120</CycleCount>
            </Battery>
          </Batteries>
        </BatteryReport>
        """;

    [Fact]
    public void Captured_report_parses_into_a_reading_and_a_result()
    {
        var reading = BatteryReport.Parse(OneBattery);

        Assert.Equal(new BatteryReading(57000, 45000), reading);
        // 45,000 / 57,000 is 78%, which the check calls attention.
        Assert.Equal(HealthStatus.Attention, HealthEvaluation.Evaluate(reading!).Status);
    }

    [Fact]
    public void The_worst_battery_in_the_report_is_the_one_reported()
    {
        var xml = OneBattery.Replace("</Battery>",
            "</Battery>\n<Battery><DesignCapacity>40000</DesignCapacity><FullChargeCapacity>15000</FullChargeCapacity></Battery>");

        var reading = BatteryReport.Parse(xml);

        Assert.Equal(new BatteryReading(40000, 15000), reading);
        Assert.Equal(HealthStatus.Problem, HealthEvaluation.Evaluate(reading!).Status);
    }

    [Fact]
    public void A_report_without_a_battery_has_no_reading()
    {
        var xml = OneBattery.Replace("<Battery>", "<NotABattery>").Replace("</Battery>", "</NotABattery>");

        Assert.Null(BatteryReport.Parse(xml));
    }

    [Fact]
    public void A_battery_without_a_full_charge_capacity_is_ignored()
    {
        var xml = OneBattery.Replace("<FullChargeCapacity>45000</FullChargeCapacity>", string.Empty);

        Assert.Null(BatteryReport.Parse(xml));
    }

    [Fact]
    public void Malformed_xml_has_no_reading() => Assert.Null(BatteryReport.Parse("not xml"));
}
