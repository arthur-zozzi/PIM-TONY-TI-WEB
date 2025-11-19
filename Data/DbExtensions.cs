// Data/DbExtensions.cs
using System.Data;

namespace TonyTI_Web.Data
{
    public static class DbExtensions
    {
        public static IDbDataParameter CreateParameterWithValue(this IDbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
            return p;
        }
    }
}
