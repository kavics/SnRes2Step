using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;

namespace SnRes2Step
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        private void convertButton_Click(object sender, RoutedEventArgs e)
        {
            Convert();
        }
        private void convertButton_LostFocus(object sender, RoutedEventArgs e)
        {
            SetMessage(DefaultMessage);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void FirePropertyChanged(string name)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        //============================================================

        const string DefaultMessage = "Fill the form and press the [Convert] button.";

        private string _resourceContentName;
        public string ResourceContentName { get { return _resourceContentName; } set { _resourceContentName = value; FirePropertyChanged("ResourceContentName"); } }

        private string _resourceClassName;
        public string ResourceClassName { get { return _resourceClassName; } set { _resourceClassName = value; FirePropertyChanged("ResourceClassName"); } }

        private string _languageCode;
        public string LanguageCode { get { return _languageCode; } set { _languageCode = value; FirePropertyChanged("LanguageCode"); } }

        private string _stringsXml;
        public string StringsXml { get { return _stringsXml; } set { _stringsXml = value; FirePropertyChanged("StringsXml"); } }

        private Visibility _messageVisibility = Visibility.Visible;
        public Visibility MessageVisibility { get { return _messageVisibility; } set { _messageVisibility = value; FirePropertyChanged("MessageVisibility"); } }

        private string _message = DefaultMessage;
        public string Message { get { return _message; } set { _message = value; FirePropertyChanged("Message"); } }

        private Visibility _errorMessageVisibility = Visibility.Hidden;
        public Visibility ErrorMessageVisibility { get { return _errorMessageVisibility; } set { _errorMessageVisibility = value; FirePropertyChanged("ErrorMessageVisibility"); } }

        private string _errorMessage;
        public string ErrorMessage { get { return _errorMessage; } set { _errorMessage = value; FirePropertyChanged("ErrorMessage"); } }


        private void SetErrorMessage(string msg)
        {
            ErrorMessage = msg;
            SetVisibility(true);
        }
        private void SetMessage(string msg)
        {
            Message = msg;
            SetVisibility(false);
        }
        private void SetVisibility(bool error)
        {
            MessageVisibility = error ? Visibility.Hidden : Visibility.Visible;
            ErrorMessageVisibility = error ? Visibility.Visible : Visibility.Hidden;
        }


        private void Convert()
        {
            SetErrorMessage(null);

            if (string.IsNullOrEmpty(StringsXml))
            {
                SetErrorMessage("Missing Strings XML.");
                return;
            }

            if (string.IsNullOrEmpty(ResourceContentName))
            {
                SetErrorMessage("Missing Resource content name.");
                return;
            }

            var parsed = Parse(stringsXmlTextBox.Text);
            if (parsed == null)
                return;

            var stepSource = ConvertToStep(parsed);

            Clipboard.SetText(stepSource);

            SetMessage("Converted step available on the Clipboard.");
        }

        private List<ResourceContent> Parse(string text)
        {
            var resContents = new List<ResourceContent>();

            text = text.Trim();
            if (text.StartsWith("<data name"))
                text = "<root>" + text + "</root>";

            var xml = new XmlDocument();
            try
            {
                xml.LoadXml(text);
            }
            catch (Exception e)
            {
                SetErrorMessage(e.Message);
                return null;
            }

            var currentLevel = xml.DocumentElement;

            if (currentLevel.Name == "Resources")
            {
                ParseResources(currentLevel, resContents);
            }
            else if (currentLevel.Name == "ResourceClass")
            {
                ParseResourceClass(currentLevel, resContents);
            }
            else
            {
                if (string.IsNullOrEmpty(ResourceClassName))
                {
                    SetErrorMessage("Missing Resource class.");
                    return null;
                }

                var content = EnsureResourceContent(ResourceContentName, ResourceClassName, resContents);

                if (currentLevel.Name == "Languages")
                {
                    ParseLanguages(currentLevel, content);
                }
                else if (currentLevel.Name == "Language")
                {
                    ParseLanguage(currentLevel, content);
                }
                else
                {
                    if (string.IsNullOrEmpty(LanguageCode))
                    {
                        SetErrorMessage("Missing Language.");
                        return null;
                    }

                    if (LanguageCode.Length != 2)
                    {
                        SetErrorMessage("Invalid Language. Use 2 character length code.");
                        return null;
                    }

                    ParseData(currentLevel, content, LanguageCode);
                }
            }

            return resContents;
        }

        private void ParseResources(XmlElement element, List<ResourceContent> resContents)
        {
            //<Resources>
            foreach (XmlElement resourceClassElement in element.SelectNodes("ResourceClass"))
                ParseResourceClass(resourceClassElement, resContents);
        }
        private void ParseResourceClass(XmlElement element, List<ResourceContent> resContents)
        {
            //  <ResourceClass name="SurveyList" >
            var className = element.Attributes["name"].Value;

            var content = EnsureResourceContent(ResourceContentName, className, resContents);

            ParseLanguages((XmlElement)element.SelectSingleNode("Languages"), content);
        }
        private void ParseLanguages(XmlElement element, ResourceContent content)
        {
            //    <Languages>
            foreach (XmlElement languageElement in element.SelectNodes("Language"))
                ParseLanguage(languageElement, content);
        }
        private void ParseLanguage(XmlElement element, ResourceContent content)
        {
            //      <Language cultureName="en">
            var lang = element.Attributes["cultureName"].Value;
            ParseData(element, content, lang);
        }
        private void ParseData(XmlElement element, ResourceContent content, string lang)
        {
            foreach (XmlElement dataElement in element.SelectNodes("data"))
                CreateResourceKey(dataElement, content, lang);
        }
        private void CreateResourceKey(XmlElement dataElement, ResourceContent content, string lang)
        {
            //        <data name="DisplayName-DisplayName" xml:space="preserve">
            //          <value>Survey title</value>
            //        </data>
            var name = dataElement.Attributes["name"].Value;
            var value = dataElement.SelectSingleNode("value").InnerText;
            content.Resources.Add(new ResourceKey { Key = name, Lang = lang, Data = value });
        }

        private ResourceContent EnsureResourceContent(string name, string className, List<ResourceContent> resContents)
        {
            var content = resContents.FirstOrDefault(c => c.Name == name && c.ClassName == className);
            if (content == null)
            {
                content = new ResourceContent { Name = name, ClassName = className };
                resContents.Add(content);
            }
            return content;
        }

        private string ConvertToStep(List<ResourceContent> parsed)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };
            var writer = XmlWriter.Create(sb, settings);

            writer.WriteStartElement("r");

            foreach (var content in parsed)
            {
                writer.WriteStartElement("AddResource");
                writer.WriteAttributeString("contentName", content.Name);
                writer.WriteAttributeString("className", content.ClassName);
                writer.WriteStartElement("Resources");

                foreach (var resource in content.Resources)
                {
                    writer.WriteStartElement("add");
                    writer.WriteAttributeString("key", resource.Key);
                    writer.WriteAttributeString("lang", resource.Lang);
                    writer.WriteString(resource.Data);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.Flush();
            writer.Close();

            var result = sb.ToString();
            result = result.Substring(3, result.Length - 7);
            return result;
        }
    }
}
