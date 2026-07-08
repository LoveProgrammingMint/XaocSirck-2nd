using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XaocSirck_Core.Engine;

// Json Model Classes
internal class Root { }

[JsonSerializable(typeof(Root))]
public partial class AppJsonContext : JsonSerializerContext { }



internal class Settings
{
    public Root? Config = JsonSerializer.Deserialize(File.ReadAllText("configs.json"), AppJsonContext.Default.Root);

    public void ReLoad()
    {
        Config = JsonSerializer.Deserialize(File.ReadAllText("configs.json"), AppJsonContext.Default.Root);
    }

    public void Save()
    {
        string jsonString = JsonSerializer.Serialize(Config, AppJsonContext.Default.Root);
        File.WriteAllText("configs.json", jsonString);
    }
}
