using System.Data;
using Microsoft.Data.SqlClient;
using MetroTechAssistant.Base;
using MetroTechAssistant.Helpers;
using MetroTechAssistant.Models;

namespace MetroTechAssistant.Repositories;

public class ManualChunkRepository : BaseRepository, IManualChunkRepo
{
    private const string Proc = "ManualChunkSP";

    public ManualChunkRepository(IConfiguration configuration) : base(configuration) { }

    private static SqlParameter Mode(string mode) =>
        new("@mode", SqlDbType.NVarChar, 20) { Value = mode };

    public async Task<List<ManualChunk>> GetAllChunksAsync()
    {
        var table = await ExecuteDataTableAsync(Proc, Mode("select"));
        var list = new List<ManualChunk>(table.Rows.Count);

        foreach (DataRow row in table.Rows)
        {
            list.Add(new ManualChunk
            {
                Id = Convert.ToInt32(row["Id"]),
                ManualName = row["ManualName"].ToString() ?? string.Empty,
                PageNo = Convert.ToInt32(row["PageNo"]),
                Content = row["Content"].ToString() ?? string.Empty,
                Embedding = row["Embedding"] is byte[] bytes ? VectorMath.ToFloats(bytes) : Array.Empty<float>()
            });
        }
        return list;
    }

    public async Task<List<ManualSummary>> GetManualSummariesAsync()
    {
        var table = await ExecuteDataTableAsync(Proc, Mode("summary"));
        var list = new List<ManualSummary>(table.Rows.Count);

        foreach (DataRow row in table.Rows)
        {
            list.Add(new ManualSummary
            {
                ManualName = row["ManualName"].ToString() ?? string.Empty,
                ChunkCount = Convert.ToInt32(row["ChunkCount"]),
                PageCount = Convert.ToInt32(row["PageCount"])
            });
        }
        return list;
    }

    public Task<int> InsertChunkAsync(ManualChunk chunk) =>
        ExecuteNonQueryAsync(Proc,
            Mode("insert"),
            new SqlParameter("@ManualName", SqlDbType.NVarChar, 200) { Value = chunk.ManualName },
            new SqlParameter("@PageNo", SqlDbType.Int) { Value = chunk.PageNo },
            new SqlParameter("@Content", SqlDbType.NVarChar, -1) { Value = chunk.Content },
            new SqlParameter("@Embedding", SqlDbType.VarBinary, -1) { Value = VectorMath.ToBytes(chunk.Embedding) });

    public Task<int> DeleteByManualAsync(string manualName) =>
        ExecuteNonQueryAsync(Proc,
            Mode("deleteByManual"),
            new SqlParameter("@ManualName", SqlDbType.NVarChar, 200) { Value = manualName });
}
