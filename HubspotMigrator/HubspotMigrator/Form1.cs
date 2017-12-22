using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Dynamic;


namespace HubspotMigrator
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
           // SyncEngagements("ae0e07b5-3c05-4bfb-b98c-f27a6b03c625", "199810d1-d2bb-4bf2-99a7-dab7f26d0808");
           
        }
       
        public string HttpGet(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            try

            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return "error";              
            }
        }
        public static string HttpPost(string Url, string Data)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(Url);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = Data;

                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
            }
            return httpResponse.ToString();
        }

        public void AddNoteToContact(int contactID, string note,string HashKey)
        {
            string url = string.Format("https://api.hubapi.com/engagements/v1/engagements?hapikey={0}", HashKey);

            dynamic jsonRequest = new ExpandoObject();
            jsonRequest.engagement = new ExpandoObject();
            jsonRequest.associations = new ExpandoObject();
            jsonRequest.engagement.active = true;
            jsonRequest.engagement.type = "NOTE";
            jsonRequest.associations.contactIds = new int[] { contactID };
            jsonRequest.metadata = new ExpandoObject();
            jsonRequest.metadata.body = note;

            string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(jsonRequest);
            HttpPost(url,jsonString);

        }
        public void AddEngagementoContact(int contactID, dynamic metadata, string type, string HashKey)
        {
            string url = string.Format("https://api.hubapi.com/engagements/v1/engagements?hapikey={0}", HashKey);

            dynamic jsonRequest = new ExpandoObject();
            jsonRequest.engagement = new ExpandoObject();
            jsonRequest.associations = new ExpandoObject();
            jsonRequest.engagement.active = true;
            jsonRequest.engagement.type = type;
            jsonRequest.associations.contactIds = new int[] { contactID };
            jsonRequest.metadata = metadata;       
            string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(jsonRequest);
            HttpPost(url, jsonString);

        }



        public void SyncEngagements(string SourceHashKey,string DestinationHashKey)
        {
            var counter = 0;
            string offset = "0";
            bool hasmore = false;

            //Fetch all source contacts
            do
            {
                if (hasmore.ToString().ToLower() == "true")
                    hasmore = true;
                else
                    hasmore = false;

                string url = string.Format("https://api.hubapi.com/contacts/v1/lists/all/contacts/all?hapikey=" + SourceHashKey + "&count=100&vidOffset=" + offset);
                string resp = HttpGet(url);
                if(resp=="error")
                {
                    MessageBox.Show("Please enter Valid API Keys");
                    btnStop.PerformClick();
                }
                else
                {
                    dynamic d = Newtonsoft.Json.JsonConvert.DeserializeObject(resp);
                    var Contacts = d.contacts;
                    hasmore = d["has-more"];
                    offset = d["vid-offset"];


                    foreach (var item in Contacts)
                    {
                        var vid = item["vid"].Value;
                        string ContactEmail = Convert.ToString(item["identity-profiles"][0].identities[0].value);
                        //check if this user exists in the destination;
                        string ContactDestinationUrl = string.Format("https://api.hubapi.com/contacts/v1/contact/email/" + ContactEmail + "/profile?hapikey=" + DestinationHashKey);
                        string destResp = HttpGet(ContactDestinationUrl);
                        if (destResp != "error")
                        {
                            dynamic dest = Newtonsoft.Json.JsonConvert.DeserializeObject(destResp);
                            var DestContactVid = dest["vid"].Value;
                            // fetch all the enagements for the current contact.
                            string EngagementsUrl = string.Format(" https://api.hubapi.com/engagements/v1/engagements/associated/CONTACT/" + vid + "/?hapikey=" + SourceHashKey);

                            string engResp = HttpGet(EngagementsUrl);
                            dynamic e = Newtonsoft.Json.JsonConvert.DeserializeObject(engResp);
                            var engagements = e.results;
                            if (engagements.Count > 0)
                            {
                                //engagements loop
                                foreach (var eng in engagements)
                                {
                                    string EngType = eng["engagement"].type;

                                    //Check engagement type
                                    if (EngType == "NOTE")
                                    {
                                        AddEngagementoContact(Convert.ToInt32(DestContactVid), eng["metadata"], "NOTE", DestinationHashKey);

                                    }
                                    else if (EngType == "EMAIL")
                                    {
                                        eng["metadata"].emailSendEventId = null;
                                        eng["metadata"].trackerKey = null;
                                        eng["metadata"].status = null;
                                        eng["metadata"].sentVia = null;

                                        AddEngagementoContact(Convert.ToInt32(DestContactVid), eng["metadata"], "EMAIL", DestinationHashKey);

                                    }

                                }
                            }
                        }
                    }
                    counter++;
                }
                
            }
            while (hasmore);
            
           
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if ((api1.Text == "") || (api2.Text == ""))
            {
                MessageBox.Show("Please Enter API Keys");
            }
            else
            {
                progressBar1.Enabled = true;
                progressBar1.Style = ProgressBarStyle.Marquee;
                api1.Enabled = false;
                api2.Enabled = false;
                btnStop.Enabled = true;
                btnSync.Enabled = false;
                SyncEngagements(api1.Text, api2.Text);

            }      
            
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            btnSync.Enabled = true;
            progressBar1.Enabled = false;
            progressBar1.Style = ProgressBarStyle.Blocks;
            api1.Enabled = true;
            api2.Enabled = true;
            btnStop.Enabled = false;
        }


    }
}
