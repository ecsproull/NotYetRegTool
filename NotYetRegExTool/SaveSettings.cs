using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace NotYetRegExTool
{
    class SettingsManager
    {
        private readonly XmlDocument settingsDoc;
        private readonly string appDataPath;
        public SettingsManager()
        {
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\NotYetRegToolSettings";
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            appDataPath = Path.Combine(appDataPath, "Settings.xml");
            settingsDoc = new XmlDocument();
            if (!File.Exists(appDataPath))
            {
                // Create the boilerplate file.
                settingsDoc.LoadXml("<Settings></Settings>");
                settingsDoc.Save(appDataPath);
            }
            else
            {
                settingsDoc.Load(appDataPath);
            }
        }

        public void SaveSetting(string groupName, string  keyName, string value)
        {
            XmlNode listCheck = settingsDoc.SelectSingleNode("/Settings/" + groupName +"/" + keyName + "[@Value='" + value + "']");
            if (listCheck != null)
            {
                return;
            }

            XmlNodeList nodes = settingsDoc.DocumentElement.GetElementsByTagName(groupName);
            XmlElement groupElement;
            if (nodes.Count == 0)
            {
                groupElement = settingsDoc.CreateElement(groupName);
                settingsDoc.DocumentElement.PrependChild(groupElement);
            }
            else
            {
                groupElement = (XmlElement)nodes[0];
            }

            XmlElement newItem = settingsDoc.CreateElement(keyName);
            newItem.SetAttribute("Value", value);
            groupElement.PrependChild(newItem);

            if (groupElement.ChildNodes.Count > 10)
            {
                groupElement.RemoveChild(groupElement.ChildNodes[10]);
            }

            settingsDoc.Save(appDataPath);
        }

        public IReadOnlyList<string> GetSetting(string groupName, string KeyName)
        {
            List<string> items = new List<string>();
            foreach (XmlElement ele in settingsDoc.GetElementsByTagName(KeyName))
            {
                items.Add(ele.GetAttribute("Value"));
            }

            return items;
        }
    }
}
