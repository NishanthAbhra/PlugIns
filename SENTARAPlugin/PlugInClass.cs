using HtmlAgilityPack;
using PlugIn4_5;
using PlugIn4_5.Common;
using RestSharp.Extensions.MonoHttp;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace SENTARAPlugin
{
    public class PlugInClass : IPlugIn
    {
        #region Global variables
        HtmlDocument _htmlDocResults = new HtmlDocument();
        HtmlDocument _TemphtmlDocResults = new HtmlDocument();
        CookieContainer cookiesresponse = new CookieContainer();
        CookieCollection cookies = new CookieCollection();
        StringBuilder _sbResponseHtml = new StringBuilder();
        StringBuilder _sbResponseText = new StringBuilder();
        List<StringBuilder> _sbResponseHtmlCollection = new List<StringBuilder>();
        List<StringBuilder> _sbResponseTextCollection = new List<StringBuilder>();
        #endregion

        #region Fetch
        public override string Fetch(DataRow dr)
        {
            Initialize(dr, true);
            string url = string.Empty;
            string output = string.Empty;
            string message = string.Empty;
            var detailsPage = "";
            string code = string.Empty;
            try
            {
                var fields = Validate();
                if (fields.IsValid)
                {
                    string firstpage = SiteCalling_HTTPPOSTANDGET("https://portalclient.echo-cloud.com/98059portal/VerifPortal/msltop.asp?id=", "GET", cookiesresponse, "");
                    if (firstpage != null && firstpage.Contains("*Required Fields"))
                    {
                        _htmlDocResults.LoadHtml(firstpage);
                        var secondpage = SiteCalling_HTTPPOSTANDGET("https://portalclient.echo-cloud.com/98059portal/VerifPortal/doclist.asp", "POST", cookiesresponse, GetParamm());
                        if (secondpage != null && !secondpage.Contains("No physicians"))
                        {
                            _htmlDocResults.LoadHtml(secondpage);
                            HtmlNodeCollection Temptrs = _htmlDocResults.DocumentNode.SelectNodes("//tr/td/a[contains(.," + provider.LastName + ")]");
                            if (Temptrs != null && Temptrs.Count() > 0)
                            {
                                int TotalRecords = Temptrs.Count();
                                foreach (HtmlNode Temptr in Temptrs)
                                {
                                    if (Temptr != null)
                                    {
                                        HtmlAttribute att = Temptr.Attributes["href"];
                                        if (att != null)
                                        {
                                            string GetUrlString = att.Value;
                                            if (!string.IsNullOrEmpty(GetUrlString))
                                            {
                                                GetUrlString = GetUrlString.Replace("JavaScript:SubmitVerif(", "").Replace(")", "").Replace("'", "");
                                                string[] Urlcode = GetUrlString.Split(',');
                                                if (Urlcode != null && Urlcode.Count() >= 4)
                                                {
                                                    var details = SiteCalling_HTTPPOSTANDGET("https://portalclient.echo-cloud.com/98059portal/VerifPortal/docset.asp?dr_id=" + HttpUtility.UrlEncode(Urlcode[0]) + "&standing=" + HttpUtility.UrlEncode(Urlcode[1]) + "&uname=Test&title=Test&org=Test&addr=Test&addr2=Test&reclink=" + HttpUtility.UrlEncode(Urlcode[2]) + "&recstat=" + HttpUtility.UrlEncode(Urlcode[3]), "GET", cookiesresponse, "");
                                                    if (details != null && details.Contains("Sentara Medical Staff Services"))
                                                    {
                                                        _TemphtmlDocResults.LoadHtml(details);
                                                        GetpageDetails();
                                                    }
                                                    else { message = ErrorMsg.CannotAccessDetailsPage; }
                                                }
                                            }
                                            else { message = ErrorMsg.Custom("JavaScript element not found"); }
                                        }
                                        else { message = ErrorMsg.Custom("Error occurred while retrieving the Anchor Node"); }
                                    }
                                    else { message = ErrorMsg.Custom("Error occurred while retrieving the data"); }
                                }
                                if (_sbResponseHtmlCollection != null && _sbResponseHtmlCollection.Count() > 0 && _sbResponseHtmlCollection.Count() == _sbResponseTextCollection.Count())
                                {
                                    int SelectedRecords = _sbResponseHtmlCollection.Count();
                                    if (SelectedRecords > 1)
                                    {
                                        _sbResponseHtml.Append("<tr><td> --- Multiple Results Found </td><td>(Total: ");
                                        _sbResponseHtml.Append(SelectedRecords.ToString());
                                        _sbResponseHtml.Append(") --- </td></tr>");
                                        _sbResponseText.Append(" --- Multiple Result Found (Total: " + SelectedRecords + ") --- ");
                                        _sbResponseText.Append('\r');
                                        _sbResponseText.Append('\n');
                                    }
                                    int Count = 1;
                                    for (int i = 0; i < SelectedRecords; i++)
                                    {

                                        if (_sbResponseHtmlCollection[i] != null && _sbResponseTextCollection[i] != null)
                                        {
                                            if (SelectedRecords > 1)
                                            {
                                                _sbResponseHtml.Append("<tr><td>--- Result </td><td>(" + Count + " Of " + SelectedRecords + ") --- </td></tr>");
                                                _sbResponseText.Append("--- Result (" + Count + " Of " + SelectedRecords + ") --- ");
                                                _sbResponseText.Append('\r');
                                                _sbResponseText.Append('\n');
                                            }
                                            _sbResponseHtml.Append(_sbResponseHtmlCollection[i]);
                                            _sbResponseText.Append(_sbResponseTextCollection[i]);
                                            Count++;
                                        }
                                    }
                                    output = _sbResponseHtml.ToString();
                                    message = _sbResponseText.ToString();
                                    try
                                    {
                                        pdf.Html = detailsPage;
                                        pdf.ConvertToABCImage(new ImageParameters { BaseUrl = "https://portalclient.echo-cloud.com/98059portal/VerifPortal/" });
                                    }
                                    catch { }
                                }
                                else
                                {
                                    message = ErrorMsg.Custom("Facility name not matching with user name");
                                }
                            }
                            else { message = ErrorMsg.NoResultsFound; }
                        }
                        else { message = ErrorMsg.NoResultsFound; }
                    }
                    else { message = ErrorMsg.CannotAccessSite; }
                }
                else { message = fields.Error.Message; }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            return ProcessResults(output, message);
        }
        #endregion

        #region Validate
        private Result<object> Validate()
        {
            if (String.IsNullOrEmpty(provider.LastName))
            {
                return Result<object>.Failure(ErrorMsg.InvalidLastName);
            }
            else if (String.IsNullOrEmpty(provider.FirstName))
            {
                return Result<object>.Failure(ErrorMsg.Custom("Invalid First Name"));
            }
            else if (String.IsNullOrEmpty(provider.GetData("orgName")))
            {
                return Result<object>.Failure(ErrorMsg.Custom("Must include organization to perform search."));
            }
            else
            {
                return Result<object>.Success(String.Empty);
            }
        }
        #endregion

        #region HTTP POST Method
        public string SiteCalling_HTTPPOSTANDGET(string url, string methodType, CookieContainer cookie, string paramss)
        {
            int count = 0;
        RETRY:
            string htmlText = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(url))
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.CookieContainer = cookie;
                    req.Method = methodType;
                    req.KeepAlive = true;
                    req.Referer = url;
                    req.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36";
                    req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3";
                    if (methodType.Equals("POST"))
                    {
                        req.ContentType = "application/x-www-form-urlencoded";
                        byte[] postData = new ASCIIEncoding().GetBytes(paramss);
                        req.ContentLength = postData.Length;
                        using (Stream newStream = req.GetRequestStream())
                        {
                            newStream.Write(postData, 0, postData.Length);
                            newStream.Flush();
                        }
                    }
                    using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                    {
                        using (StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        {
                            htmlText = sr.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                count++;
                if (count > 6)
                    htmlText = ex.Message;
                else
                    goto RETRY;
            }
            return htmlText;
        }
        #endregion

        #region GetpageDetails
        private void GetpageDetails()
        {
            HtmlNode commonNode = null;
            StringBuilder singleResponseHtml = new StringBuilder();
            StringBuilder singleResponseText = new StringBuilder();
            try
            {
                if (_TemphtmlDocResults != null && _TemphtmlDocResults.DocumentNode != null)
                {
                    HtmlNodeCollection trs = _TemphtmlDocResults.DocumentNode.SelectNodes("//table[contains(.,'Original Appointment Date:')]/tr[position() > 1]");
                    if (trs != null && trs.Count > 0)
                    {
                        bool flag = true;
                        bool FacilityCheck = false;
                        foreach (HtmlNode tr in trs)
                        {
                            commonNode = tr.SelectSingleNode(".//td[1]");
                            if (commonNode != null && !string.IsNullOrEmpty(commonNode.InnerText) && flag)
                            {
                                singleResponseText.Append("Name: ");
                                singleResponseHtml.Append("<tr><td>Name: </td><td>");
                                if (commonNode != null && !string.IsNullOrEmpty(commonNode.InnerText))
                                {
                                    singleResponseHtml.Append(commonNode.InnerText.Replace("&nbsp", "").Replace(";", "").Trim().Substring(3));
                                    singleResponseText.Append(commonNode.InnerText.Replace("&nbsp", "").Replace(";", "").Trim().Substring(3));
                                }
                                singleResponseHtml.Append("</td></tr>");
                                singleResponseText.Append('\r');
                                singleResponseText.Append('\n');
                            }
                            else if (commonNode != null && !string.IsNullOrEmpty(commonNode.InnerText))
                            {

                                if (tr.InnerText.Contains("Facility"))
                                {
                                    if (tr.InnerText.Contains(provider.GetData("orgName")))
                                    { FacilityCheck = true; }
                                    else
                                    {
                                        FacilityCheck = false;
                                    }
                                }
                                if (FacilityCheck)
                                {
                                    singleResponseText.Append(commonNode.InnerText.Replace("&nbsp", "").Replace(";", "").Trim() + " ");
                                    singleResponseHtml.Append("<tr><td>" + commonNode.InnerText.Replace("&nbsp", "").Replace(";", "").Trim() + " </td><td>");

                                    commonNode = tr.SelectSingleNode(".//td[2]");
                                    if (commonNode != null && !string.IsNullOrEmpty(commonNode.InnerText))
                                    {
                                        singleResponseHtml.Append(commonNode.InnerText.Replace("&nbsp", "").Replace(";", "").Trim());
                                        singleResponseText.Append(commonNode.InnerText.Replace("&nbsp", "").Replace(";", "").Trim());
                                    }
                                    singleResponseHtml.Append("</td></tr>");
                                    singleResponseText.Append('\r');
                                    singleResponseText.Append('\n');
                                }

                            }
                            flag = false;
                        }
                        if (singleResponseHtml.ToString().Contains(provider.GetData("orgName")))
                        {
                            _sbResponseHtmlCollection.Add(singleResponseHtml);
                            _sbResponseTextCollection.Add(singleResponseText);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion

        #region GetParams
        public string GetParamm()
        {
            try
            {
                string returnParmm = "LASTNAME=" + provider.LastName + "&FIRSTNAME=" + provider.FirstName + "&Button1=Click+Here+To+Search&U_FULLNAME=Test&U_TITLE=Test&U_ORG=Test&U_ADDR=Test&U_ADDR2=Test";

                return returnParmm;

            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
        #endregion
    }
}









