namespace Core.Models;

using Parquet.Schema;
using Parquet.Data;

// Example schema of PointstoPoints
public static class RoutingRow
{
    public static readonly ParquetSchema Schema = new(
        new DataField<float>("Duration"),
        new DataField<float>("Distance"));

    public static DataColumn[] ToColumns(float[] durations, float[] distances) =>
    [
        new DataColumn(Schema.DataFields[0], durations),
        new DataColumn(Schema.DataFields[1], distances),
    ];
}