using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.CustomCosmetics.ShareCosmetics;

class HttpConnect
{
    public static async Task<string> Download(string url)
    {
        HttpClient http = new();
        http.DefaultRequestHeaders.Add("User-Agent", "TOHEK CustomCosmetics");
        var response = await http.GetAsync(new System.Uri(url), HttpCompletionOption.ResponseContentRead);
        if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
        {
            System.Console.WriteLine("Server returned no data: " + response.StatusCode.ToString());
            return "";
        }
        return await response.Content.ReadAsStringAsync();
    }
    public static async Task<bool> ShareCosmeticDateDownload(byte id, string url)
    {
        var dldata = await Download(url);

        Main.Logger.LogInfo("DLDATA:" + dldata);
        SharePatch.PlayerData[id] = dldata;
        Main.Logger.LogInfo("c");
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(dldata));
        Main.Logger.LogInfo("e");
        var serializer = new DataContractJsonSerializer(typeof(CosmeticsObject));
        Main.Logger.LogInfo("f");
        var data = serializer.ReadObject(ms);
        Main.Logger.LogInfo("g");
        SharePatch.PlayerObjects[id] = (CosmeticsObject)data;
        Main.Logger.LogInfo("h");
        return false;
    }
}