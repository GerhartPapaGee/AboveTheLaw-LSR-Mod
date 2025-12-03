using System;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

public class SettingsManager
{
    private string SettingsFile;

    public Keys MenuKey { get; set; } = Keys.F3;
    public Keys PullOverKey { get; set; } = Keys.E;
    public Keys CuffKey { get; set; } = Keys.T;
    public Keys BackseatKey { get; set; } = Keys.Y;

    public SettingsManager(string folder)
    {
        SettingsFile = Path.Combine(folder, "AboveTheLaw_Settings.xml");
    }

    public void LoadSettings()
    {
        if (!File.Exists(SettingsFile))
        {
            SaveSettings();
            return;
        }

        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(SettingsManager));
            using (FileStream fs = new FileStream(SettingsFile, FileMode.Open))
            {
                SettingsManager loaded = (SettingsManager)serializer.Deserialize(fs);
                MenuKey = loaded.MenuKey;
                PullOverKey = loaded.PullOverKey;
                CuffKey = loaded.CuffKey;
                BackseatKey = loaded.BackseatKey;
            }
        }
        catch { SaveSettings(); }
    }

    public void SaveSettings()
    {
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(SettingsManager));
            using (FileStream fs = new FileStream(SettingsFile, FileMode.Create))
            {
                serializer.Serialize(fs, this);
            }
        }
        catch { }
    }
}