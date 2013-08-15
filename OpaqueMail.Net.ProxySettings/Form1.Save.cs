﻿using Microsoft.Win32;
using OpaqueMail.Net.Proxy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

namespace OpaqueMail.Net.ProxySettings
{
    public partial class Form1
    {
        /// <summary>
        /// Handle the save event and update OpaqueMail Proxy's settings..
        /// </summary>
        private void SaveSettingsButton_Click(object sender, EventArgs e)
        {
            XPathDocument document = null;

            if (File.Exists(SettingsFileName))
            {
                DialogResult dr = MessageBox.Show("A settings file already exists for OpaqueMail Proxy.  Overwrite with these settings?", "Overwrite OpaqueMail Proxy Settings?", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (dr != System.Windows.Forms.DialogResult.OK)
                    return;

                try
                {
                    document = new XPathDocument(SettingsFileName);
                }
                catch { }
            }

            // If the service is running, stop it before proceeding.
            if (ServiceExists("OpaqueMailProxy"))
            {
                ServiceController serviceContoller = new ServiceController("OpaqueMailProxy");
                if (serviceContoller.Status != ServiceControllerStatus.Stopped && serviceContoller.Status != ServiceControllerStatus.StopPending)
                    serviceContoller.Stop();
            }

            List<ProxyAccount> accounts = new List<ProxyAccount>();

            // First, account for any settings in the existing XML file.
            string fqdn = Functions.GetLocalFQDN();

            int smtpServiceCount = 0, imapServiceCount = 0, pop3ServiceCount = 0;
            if (document != null)
            {
                XPathNavigator navigator = document.CreateNavigator();

                smtpServiceCount = GetXmlIntValue(navigator, "/Settings/SMTP/ServiceCount") ?? 0;
                for (int i = 1; i <= smtpServiceCount; i++)
                {
                    ProxyAccount account = new ProxyAccount();
                    account.LocalSmtpIPAddress = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/LocalIPAddress") ?? "";
                    account.LocalSmtpPort = GetXmlIntValue(navigator, "/Settings/SMTP/Service" + i + "/LocalPort") ?? 587;
                    account.LocalSmtpEnableSsl = GetXmlBoolValue(navigator, "/Settings/SMTP/Service" + i + "/LocalEnableSsl") ?? true;
                    account.RemoteSmtpServer = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/RemoteServerHostName");
                    account.RemoteSmtpPort = GetXmlIntValue(navigator, "/Settings/SMTP/Service" + i + "/RemoteServerPort") ?? 587;
                    account.RemoteSmtpEnableSsl = GetXmlBoolValue(navigator, "/Settings/SMTP/Service" + i + "/RemoteServerEnableSSL") ?? true;
                    account.RemoteSmtpUsername = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/RemoteServerUsername");
                    account.RemoteSmtpPassword = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/RemoteServerPassword");
                    account.RemoteSmtpFrom = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/RemoteServerFrom");
                    account.RemoteSmtpTo = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/RemoteServerTo");
                    account.SmtpAcceptedIPs = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/AcceptedIPs");
                    account.SmtpCertificateLocation = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/Certificate/Location");
                    account.SmtpCertificateSerialNumber = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/Certificate/SerialNumber");
                    account.SmtpCertificateSubjectName = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/Certificate/SubjectName");
                    account.SmtpLogFile = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/LogFile");

                    string logLevel = ProxyFunctions.GetXmlStringValue(navigator, "Settings/SMTP/Service" + i + "/LogLevel");
                    switch (logLevel.ToUpper())
                    {
                        case "NONE":
                            account.SmtpLogLevel = LogLevel.None;
                            break;
                        case "CRITICAL":
                            account.SmtpLogLevel = LogLevel.Critical;
                            break;
                        case "ERROR":
                            account.SmtpLogLevel = LogLevel.Error;
                            break;
                        case "RAW":
                            account.SmtpLogLevel = LogLevel.Raw;
                            break;
                        case "VERBOSE":
                            account.SmtpLogLevel = LogLevel.Verbose;
                            break;
                        case "WARNING":
                            account.SmtpLogLevel = LogLevel.Warning;
                            break;
                        case "INFORMATION":
                        default:
                            account.SmtpLogLevel = LogLevel.Information;
                            break;
                    }

                    account.SendCertificateReminders = GetXmlBoolValue(navigator, "/Settings/SMTP/Service" + i + "/SendCertificateReminders") ?? true;
                    account.SmimeEncrypt = GetXmlBoolValue(navigator, "/Settings/SMTP/Service" + i + "/SMIMEEncrypt") ?? true;
                    account.SmimeRemovePreviousOperations = GetXmlBoolValue(navigator, "/Settings/SMTP/Service" + i + "/SMIMERemovePreviousOperations") ?? true;
                    account.SmimeSign = GetXmlBoolValue(navigator, "/Settings/SMTP/Service" + i + "/SMIMESign") ?? true;
                    account.SmimeTripleWrap = GetXmlBoolValue(navigator, "/Settings/SMTP/Service" + i + "/SMIMETripleWrap") ?? true;

                    int? registryKeyCount = GetXmlIntValue(navigator, "/Settings/SMTP/Service" + i + "/OutlookRegistryKeyCount") ?? 0;
                    for (int j = 1; j <= registryKeyCount; j++)
                    {
                        string registryKey = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/OutlookRegistryKey" + j);
                        if (!account.OutlookRegistryKeys.Contains(registryKey))
                            account.OutlookRegistryKeys.Add(registryKey);
                    }

                    int? thunderbirdKeyCount = GetXmlIntValue(navigator, "/Settings/SMTP/Service" + i + "/ThunderbirdKeyCount") ?? 0;
                    for (int j = 1; j <= registryKeyCount; j++)
                    {
                        string thunderbirdKey = GetXmlStringValue(navigator, "/Settings/SMTP/Service" + i + "/ThunderbirdKey" + j);
                        if (!string.IsNullOrEmpty(thunderbirdKey) && !account.ThunderbirdKeys.Contains(thunderbirdKey))
                            account.ThunderbirdKeys.Add(thunderbirdKey);
                    }

                    accounts.Add(account);
                }

                imapServiceCount = GetXmlIntValue(navigator, "/Settings/IMAP/ServiceCount") ?? 0;
                for (int i = 1; i <= imapServiceCount; i++)
                {
                    ProxyAccount account = new ProxyAccount();
                    bool accountMatched = false;

                    // Check if a matching Outlook account already exists.
                    int? registryKeyCount = GetXmlIntValue(navigator, "/Settings/IMAP/Service" + i + "/OutlookRegistryKeyCount") ?? 0;
                    for (int j = 1; j <= registryKeyCount; j++)
                    {
                        string registryKey = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/OutlookRegistryKey" + j);
                        if (!account.OutlookRegistryKeys.Contains(registryKey))
                            account.OutlookRegistryKeys.Add(registryKey);

                        foreach (ProxyAccount existingAccount in accounts)
                        {
                            if (existingAccount.OutlookRegistryKeys.Contains(registryKey) && !accountMatched)
                            {
                                account = existingAccount;
                                j = 0;
                                accountMatched = true;
                            }
                        }
                    }

                    // Check if a matching Thunderbird account already exists.
                    int? thunderbirdKeyCount = GetXmlIntValue(navigator, "/Settings/IMAP/Service" + i + "/ThunderbirdKeyCount") ?? 0;
                    for (int j = 1; j <= registryKeyCount; j++)
                    {
                        string thunderbirdKey = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/ThunderbirdKey" + j);
                        if (!string.IsNullOrEmpty(thunderbirdKey) && !account.ThunderbirdKeys.Contains(thunderbirdKey))
                            account.ThunderbirdKeys.Add(thunderbirdKey);

                        foreach (ProxyAccount existingAccount in accounts)
                        {
                            if (existingAccount.ThunderbirdKeys.Contains(thunderbirdKey) && !accountMatched)
                            {
                                account = existingAccount;
                                j = 0;
                                accountMatched = true;
                            }
                        }
                    }

                    account.LocalImapIPAddress = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/LocalIPAddress") ?? "";
                    account.LocalImapPort = GetXmlIntValue(navigator, "/Settings/IMAP/Service" + i + "/LocalPort") ?? 587;
                    account.LocalImapEnableSsl = GetXmlBoolValue(navigator, "/Settings/IMAP/Service" + i + "/LocalEnableSsl") ?? true;

                    account.RemoteImapServer = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/RemoteServerHostName");
                    account.RemoteImapPort = GetXmlIntValue(navigator, "/Settings/IMAP/Service" + i + "/RemoteServerPort") ?? 993;
                    account.RemoteImapEnableSsl = GetXmlBoolValue(navigator, "/Settings/IMAP/Service" + i + "/RemoteServerEnableSSL") ?? true;
                    account.ImapAcceptedIPs = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/AcceptedIPs");
                    account.ImapCertificateLocation = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/Certificate/Location");
                    account.ImapCertificateSerialNumber = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/Certificate/SerialNumber");
                    account.ImapCertificateSubjectName = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/Certificate/SubjectName");
                    account.ImapLogFile = GetXmlStringValue(navigator, "/Settings/IMAP/Service" + i + "/LogFile");

                    string logLevel = ProxyFunctions.GetXmlStringValue(navigator, "Settings/IMAP/Service" + i + "/LogLevel");
                    switch (logLevel.ToUpper())
                    {
                        case "NONE":
                            account.ImapLogLevel = LogLevel.None;
                            break;
                        case "CRITICAL":
                            account.ImapLogLevel = LogLevel.Critical;
                            break;
                        case "ERROR":
                            account.ImapLogLevel = LogLevel.Error;
                            break;
                        case "RAW":
                            account.ImapLogLevel = LogLevel.Raw;
                            break;
                        case "VERBOSE":
                            account.ImapLogLevel = LogLevel.Verbose;
                            break;
                        case "WARNING":
                            account.ImapLogLevel = LogLevel.Warning;
                            break;
                        case "INFORMATION":
                        default:
                            account.ImapLogLevel = LogLevel.Information;
                            break;
                    }
                }

                // Handle POP3 settings third.
                pop3ServiceCount = GetXmlIntValue(navigator, "/Settings/POP3/ServiceCount") ?? 0;
                for (int i = 1; i <= pop3ServiceCount; i++)
                {
                    ProxyAccount account = new ProxyAccount();
                    bool accountMatched = false;

                    // Check if a matching Outlook account already exists.
                    int? registryKeyCount = GetXmlIntValue(navigator, "/Settings/POP3/Service" + i + "/OutlookRegistryKeyCount") ?? 0;
                    for (int j = 1; j <= registryKeyCount; j++)
                    {
                        string registryKey = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/OutlookRegistryKey" + j);
                        if (!account.OutlookRegistryKeys.Contains(registryKey))
                            account.OutlookRegistryKeys.Add(registryKey);

                        foreach (ProxyAccount existingAccount in accounts)
                        {
                            if (existingAccount.OutlookRegistryKeys.Contains(registryKey) && !accountMatched)
                            {
                                account = existingAccount;
                                j = 0;
                                accountMatched = true;
                            }
                        }
                    }

                    // Check if a matching Thunderbird account already exists.
                    int? thunderbirdKeyCount = GetXmlIntValue(navigator, "/Settings/POP3/Service" + i + "/ThunderbirdKeyCount") ?? 0;
                    for (int j = 1; j <= registryKeyCount; j++)
                    {
                        string thunderbirdKey = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/ThunderbirdKey" + j);
                        if (!string.IsNullOrEmpty(thunderbirdKey) && !account.ThunderbirdKeys.Contains(thunderbirdKey))
                            account.ThunderbirdKeys.Add(thunderbirdKey);

                        foreach (ProxyAccount existingAccount in accounts)
                        {
                            if (existingAccount.ThunderbirdKeys.Contains(thunderbirdKey) && !accountMatched)
                            {
                                account = existingAccount;
                                j = 0;
                                accountMatched = true;
                            }
                        }
                    }

                    account.LocalPop3IPAddress = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/LocalIPAddress") ?? "";
                    account.LocalPop3Port = GetXmlIntValue(navigator, "/Settings/POP3/Service" + i + "/LocalPort") ?? 995;
                    account.LocalPop3EnableSsl = GetXmlBoolValue(navigator, "/Settings/POP3/Service" + i + "/LocalEnableSsl") ?? true;

                    account.RemotePop3Server = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/RemoteServerHostName");
                    account.RemotePop3Port = GetXmlIntValue(navigator, "/Settings/POP3/Service" + i + "/RemoteServerPort") ?? 995;
                    account.RemotePop3EnableSsl = GetXmlBoolValue(navigator, "/Settings/POP3/Service" + i + "/RemoteServerEnableSSL") ?? true;
                    account.Pop3AcceptedIPs = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/AcceptedIPs");
                    account.Pop3CertificateLocation = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/Certificate/Location");
                    account.Pop3CertificateSerialNumber = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/Certificate/SerialNumber");
                    account.Pop3CertificateSubjectName = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/Certificate/SubjectName");
                    account.Pop3LogFile = GetXmlStringValue(navigator, "/Settings/POP3/Service" + i + "/LogFile");

                    string logLevel = ProxyFunctions.GetXmlStringValue(navigator, "Settings/POP3/Service" + i + "/LogLevel");
                    switch (logLevel.ToUpper())
                    {
                        case "NONE":
                            account.Pop3LogLevel = LogLevel.None;
                            break;
                        case "CRITICAL":
                            account.Pop3LogLevel = LogLevel.Critical;
                            break;
                        case "ERROR":
                            account.Pop3LogLevel = LogLevel.Error;
                            break;
                        case "RAW":
                            account.Pop3LogLevel = LogLevel.Raw;
                            break;
                        case "VERBOSE":
                            account.Pop3LogLevel = LogLevel.Verbose;
                            break;
                        case "WARNING":
                            account.Pop3LogLevel = LogLevel.Warning;
                            break;
                        case "INFORMATION":
                        default:
                            account.Pop3LogLevel = LogLevel.Information;
                            break;
                    }
                }
            }

            // Second, gather existing Outlook account settings from the registry.
            foreach (string outlookVersion in OutlookVersions.Keys)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + outlookVersion + @"\Outlook\Profiles\Outlook\9375CFF0413111d3B88A00104B2A6676", false))
                {
                    if (key != null)
                    {
                        string[] subkeyNames = key.GetSubKeyNames();
                        if (subkeyNames != null)
                        {
                            foreach (string subkeyName in subkeyNames)
                            {
                                using (RegistryKey subKey = key.OpenSubKey(subkeyName, false))
                                {
                                    bool matched = false;
                                    foreach (ProxyAccount existingAccount in accounts)
                                    {
                                        if (existingAccount.OutlookRegistryKeys.Contains(subKey.Name))
                                            matched = true;
                                    }

                                    if (!matched)
                                    {
                                        ProxyAccount account = new ProxyAccount();
                                        account.ClientType = "Outlook";
                                        account.ClientVersion = outlookVersion;

                                        account.RemoteImapEnableSsl = GetOutlookRegistryValue(subKey, "IMAP Use SSL") == "1";
                                        int.TryParse(GetOutlookRegistryValue(subKey, "IMAP Port"), out account.RemoteImapPort);
                                        account.RemoteImapServer = GetOutlookRegistryValue(subKey, "IMAP Server") ?? "";

                                        account.RemotePop3EnableSsl = GetOutlookRegistryValue(subKey, "POP3 Use SSL") == "1";
                                        int.TryParse(GetOutlookRegistryValue(subKey, "POP3 Port"), out account.RemotePop3Port);
                                        account.RemotePop3Server = GetOutlookRegistryValue(subKey, "POP3 Server") ?? "";

                                        account.RemoteSmtpEnableSsl = GetOutlookRegistryValue(subKey, "SMTP Use SSL") == "1";
                                        int.TryParse(GetOutlookRegistryValue(subKey, "SMTP Port"), out account.RemoteSmtpPort);
                                        account.RemoteSmtpServer = GetOutlookRegistryValue(subKey, "SMTP Server") ?? "";

                                        // Only proceed if a server is found.
                                        if (!string.IsNullOrEmpty(account.RemoteImapServer) || !string.IsNullOrEmpty(account.RemotePop3Server) || !string.IsNullOrEmpty(account.RemoteSmtpServer))
                                        {
                                            string username = GetOutlookRegistryValue(subKey, "IMAP User");
                                            if (string.IsNullOrEmpty(username))
                                                username = GetOutlookRegistryValue(subKey, "POP3 User");

                                            if (!string.IsNullOrEmpty(username))
                                            {
                                                account.Usernames.Add(username);
                                                account.OutlookRegistryKeys.Add(subKey.Name);

                                                accounts.Add(account);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Third, gather existing Thunderbird account settings.
            if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Thunderbird\\Profiles"))
            {
                foreach (string directory in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Thunderbird\\Profiles"))
                {
                    if (File.Exists(directory + "\\prefs.js"))
                    {
                        string prefsFile = File.ReadAllText(directory + "\\prefs.js");

                        int keyCount;
                        int.TryParse(Functions.ReturnBetween(prefsFile, "user_pref(\"mail.account.lastKey\", ", ")"), out keyCount);
                        for (int i = 1; i <= keyCount; i++)
                        {
                            string thunderbirdKey = directory + "~" + i.ToString();

                            bool matched = false;
                            foreach (ProxyAccount existingAccount in accounts)
                            {
                                if (existingAccount.ThunderbirdKeys.Contains(thunderbirdKey))
                                    matched = true;
                            }

                            if (!matched)
                            {
                                ProxyAccount account = new ProxyAccount();
                                account.ClientType = "Thunderbird";

                                int sslValue = 0;
                                int.TryParse(Functions.ReturnBetween(prefsFile, "user_pref(\"mail.smtpserver.smtp" + i.ToString() + ".try_ssl\", ", ")"), out sslValue);
                                account.RemoteSmtpEnableSsl = sslValue > 0;
                                int.TryParse(Functions.ReturnBetween(prefsFile, "user_pref(\"mail.smtpserver.smtp" + i.ToString() + ".port\", ", ")"), out account.RemoteSmtpPort);
                                account.RemoteSmtpServer = Functions.ReturnBetween(prefsFile, "user_pref(\"mail.smtpserver.smtp" + i.ToString() + ".hostname\", \"", "\"") ?? "";

                                if (Functions.ReturnBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".type\", \"", "\"") == "pop3")
                                {
                                    int.TryParse(Functions.ReturnBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".port\", ", ")"), out account.RemotePop3Port);
                                    account.RemotePop3EnableSsl = (account.RemotePop3Port == 995);
                                    account.RemotePop3Server = Functions.ReturnBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".hostname\", \"", "\"") ?? "";
                                }
                                else
                                {
                                    int.TryParse(Functions.ReturnBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".port\", ", ")"), out account.RemoteImapPort);
                                    account.RemoteImapEnableSsl = (account.RemoteImapPort == 993);
                                    account.RemoteImapServer = Functions.ReturnBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".hostname\", \"", "\"") ?? "";
                                }

                                // Only proceed if a server is found.
                                if (!string.IsNullOrEmpty(account.RemoteImapServer) || !string.IsNullOrEmpty(account.RemotePop3Server) || !string.IsNullOrEmpty(account.RemoteSmtpServer))
                                {
                                    string username = Functions.ReturnBetween(prefsFile, "user_pref(\"mail.smtpserver.smtp" + i.ToString() + ".username\", \"", "\"");

                                    if (!string.IsNullOrEmpty(username))
                                    {
                                        account.Usernames.Add(username);
                                        account.ThunderbirdKeys.Add(thunderbirdKey);

                                        accounts.Add(account);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Fourth, check which accounts the user chooses to encrypt.
            smtpServiceCount = 0;
            imapServiceCount = 0;
            pop3ServiceCount = 0;

            HashSet<int> portsReserved = new HashSet<int>();
            int nextPortToTry = 1000;

            foreach (DataGridViewRow row in AccountGrid.Rows)
            {
                if ((bool)row.Cells[2].Value == true)
                {
                    foreach (ProxyAccount account in accounts)
                    {
                        if ((account.OutlookRegistryKeys.Contains((string)row.Cells[4].Value) || account.ThunderbirdKeys.Contains((string)row.Cells[4].Value)) && !account.Matched)
                        {
                            account.Matched = true;

                            // Ensure the SMTP proxy connection has a unique port.
                            if (!string.IsNullOrEmpty(account.RemoteSmtpServer))
                            {
                                smtpServiceCount++;

                                if (portsReserved.Contains(account.LocalSmtpPort))
                                {
                                    nextPortToTry = GetNextAvailablePort(++nextPortToTry);
                                    account.LocalSmtpPort = nextPortToTry;
                                }

                                portsReserved.Add(account.LocalSmtpPort);
                            }

                            // Ensure the IMAP proxy connection has a unique port.
                            if (!string.IsNullOrEmpty(account.RemoteImapServer))
                            {
                                imapServiceCount++;

                                if (portsReserved.Contains(account.LocalImapPort))
                                {
                                    nextPortToTry = GetNextAvailablePort(++nextPortToTry);
                                    account.LocalImapPort = nextPortToTry;
                                }

                                portsReserved.Add(account.LocalImapPort);
                            }

                            // Ensure the POP3 proxy connection has a unique port.
                            if (!string.IsNullOrEmpty(account.RemotePop3Server))
                            {
                                pop3ServiceCount++;

                                if (portsReserved.Contains(account.LocalPop3Port))
                                {
                                    nextPortToTry = GetNextAvailablePort(++nextPortToTry);
                                    account.LocalPop3Port = nextPortToTry;
                                }

                                portsReserved.Add(account.LocalPop3Port);
                            }
                        }
                    }
                }
            }

            // Fifth, write out the XML setting values.
            XmlWriterSettings streamWriterSettings = new XmlWriterSettings();
            streamWriterSettings.Indent = true;
            streamWriterSettings.IndentChars = "  ";
            streamWriterSettings.NewLineChars = "\r\n";
            streamWriterSettings.NewLineHandling = NewLineHandling.Replace;

            // Determine default accepted IPs.
            string defaultAcceptedIPs = "0.0.0.0";
            if (NetworkAccess.SelectedIndex == 1)
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress hostIP in hostEntry.AddressList)
                {
                    string[] ipParts = hostIP.ToString().Split('.');
                    if (ipParts.Length > 2)
                    {
                        defaultAcceptedIPs = ipParts[0] + "." + ipParts[1] + ".*";
                        break;
                    }
                }
            }
            else if (NetworkAccess.SelectedIndex == 2)
                defaultAcceptedIPs = "*";

            using (XmlWriter streamWriter = XmlWriter.Create(SettingsFileName, streamWriterSettings))
            {
                streamWriter.WriteStartDocument();

                streamWriter.WriteStartElement("Settings");

                streamWriter.WriteStartElement("SMTP");

                streamWriter.WriteComment("The number of SMTP proxy services to run.  Each proxy's settings will be outlined in subsequent <Service#/> blocks.");
                streamWriter.WriteElementString("ServiceCount", smtpServiceCount.ToString());

                int smtpServiceId = 0;
                foreach (ProxyAccount account in accounts)
                {
                    if (!string.IsNullOrEmpty(account.RemoteSmtpServer) && account.Matched)
                    {
                        streamWriter.WriteStartElement("Service" + (++smtpServiceId).ToString());

                        streamWriter.WriteComment("IP addresses to accept connections from.  Delete or set value to \"*\" to accept connections from any IP.");
                        streamWriter.WriteComment("Individual IPs can be specified, separated by commas, or ranges can be specified.  The \"*\" wildcard character is supported.");
                        streamWriter.WriteComment("By default, connections are only accepted from the localhost.");
                        streamWriter.WriteElementString("AcceptedIPs", account.ImapAcceptedIPs ?? defaultAcceptedIPs);

                        streamWriter.WriteComment("Local IP address to listen on.  \"Any\" means listen on all IPs.");
                        streamWriter.WriteElementString("LocalIPAddress", account.LocalSmtpIPAddress ?? "Any");
                        streamWriter.WriteComment("Local port to listen on.");
                        streamWriter.WriteElementString("LocalPort", account.LocalSmtpPort > 0 ? account.LocalSmtpPort.ToString() : "587");
                        streamWriter.WriteComment("Whether local connections support TLS/SSL protection.");
                        streamWriter.WriteElementString("LocalEnableSSL", account.LocalSmtpEnableSsl.ToString());

                        streamWriter.WriteComment("Remote SMTP server hostname to connect to.  Common values: smtp.gmail.com, smtp.live.com, smtp.mail.yahoo.com");
                        streamWriter.WriteElementString("RemoteServerHostName", account.RemoteSmtpServer ?? "Any");
                        streamWriter.WriteComment("Remote SMTP server port to connect to.  587 is recommended, but 465 or 25 may be required.");
                        streamWriter.WriteElementString("RemoteServerPort", account.RemoteSmtpPort > 0 ? account.RemoteSmtpPort.ToString() : "587");
                        streamWriter.WriteComment("Whether the remote SMTP server supports TLS/SSL protection.");
                        streamWriter.WriteElementString("RemoteServerEnableSSL", account.RemoteSmtpEnableSsl.ToString());

                        streamWriter.WriteComment("(Optional) Username used when authenticating to the remote SMTP server.  When supplied, it will override any values sent from the client.");
                        streamWriter.WriteElementString("RemoteServerUsername", account.RemoteSmtpUsername);
                        streamWriter.WriteComment("(Optional) Password used when authenticating to the remote SMTP server.  When supplied, it will override any values sent from the client.");
                        streamWriter.WriteElementString("RemoteServerPassword", account.RemoteSmtpPassword);
                        streamWriter.WriteComment("(Optional) \"From\" address for all sent messages.  When supplied, it will override any values sent from the client.");
                        streamWriter.WriteElementString("RemoteServerFrom", account.RemoteSmtpFrom);
                        streamWriter.WriteComment("(Optional) \"To\" address for all sent messages.  When supplied, it will override any values sent from the client.");
                        streamWriter.WriteElementString("RemoteServerTo", account.RemoteSmtpTo);

                        streamWriter.WriteStartElement("Certificate");
                        streamWriter.WriteComment("Where certificates should be stored and retrieved from by default.  \"LocalMachine\" or \"CurrentUser\" only.");
                        streamWriter.WriteElementString("Location", account.SmtpCertificateLocation ?? "LocalMachine");
                        streamWriter.WriteComment("(Optional) The serial number of an X509 certificate to be used for server identification.  If left blank, one will be autogenerated.");
                        streamWriter.WriteElementString("SerialNumber", account.SmtpCertificateSerialNumber);
                        streamWriter.WriteComment("(Optional) The subject name of an X509 certificate to be used for server identification.  If left blank, one will be autogenerated.");
                        streamWriter.WriteElementString("SubjectName", account.SmtpCertificateSubjectName);
                        streamWriter.WriteEndElement();

                        streamWriter.WriteComment("Send e-mail reminders when a signing certificate is due to expire within 30 days.");
                        streamWriter.WriteElementString("SendCertificateReminders", account.SendCertificateReminders.ToString());

                        streamWriter.WriteComment("Whether all outgoing messages require the S/MIME settings specified below.");
                        streamWriter.WriteComment("When set to \"RequireExactSettings\", any messages that can't be signed or encrypted will be dropped, unsent.");
                        streamWriter.WriteComment("When set to \"BestEffort\", OpaqueMail Proxy will attempt to sign and/or encrypt messages but still forward any that can't be.");
                        streamWriter.WriteElementString("SMIMESettingsMode", SmimeSettingsMode.SelectedIndex > 0 ? "RequireExactSettings" : "BestEffort");

                        streamWriter.WriteComment("Whether to sign the e-mail.  When true, signing is the first S/MIME operation.");
                        streamWriter.WriteElementString("SMIMESign", SmimeOperations.SelectedIndex > 0 ? "True" : "False");
                        streamWriter.WriteComment("Whether to encrypt the e-mail's envelope.  When SmimeSign is true, encryption is the second S/MIME operation.");
                        streamWriter.WriteElementString("SMIMEEncrypt", SmimeOperations.SelectedIndex > 1 ? "True" : "False");
                        streamWriter.WriteComment("Triple-wrap the e-mail by signing, then encrypting the envelope, then signing the encrypted envelope.");
                        streamWriter.WriteElementString("SMIMETripleWrap", SmimeOperations.SelectedIndex > 2 ? "True" : "False");

                        streamWriter.WriteComment("Remove envelope encryption and signatures from passed-in messages.  If true and SmimeSigned or SmimeEncryptEnvelope is also true, new S/MIME operations will be applied.");
                        streamWriter.WriteElementString("SMIMERemovePreviousOperations", account.SmimeRemovePreviousOperations.ToString());

                        streamWriter.WriteComment("Where log files should be stored, if any.  Leave blank to avoid logging.");
                        streamWriter.WriteComment("Date and instance variables can be encased in angle braces.  For example, \"Logs\\SMTPProxy{#}-{yyyy-MM-dd}.log\".");
                        streamWriter.WriteElementString("LogFile", account.SmtpLogFile ?? "Logs\\SMTPProxy{#}-{yyyy-MM-dd}.log");

                        streamWriter.WriteComment("Proxy logging level, determining how much information is logged.  Possible values: None, Critical, Error, Warning, Information, Verbose, Raw");
                        streamWriter.WriteElementString("LogLevel", account.SmtpLogLevel.ToString());

                        if (account.OutlookRegistryKeys.Count > 0)
                        {
                            streamWriter.WriteComment("Outlook registry keys for accounts configured through the OpaqueMail Proxy settings app.");
                            streamWriter.WriteElementString("OutlookRegistryKeyCount", account.OutlookRegistryKeys.Count.ToString());

                            int registryKeyId = 0;
                            foreach (string registryKey in account.OutlookRegistryKeys)
                                streamWriter.WriteElementString("OutlookRegistryKey" + (++registryKeyId).ToString(), registryKey);
                        }

                        if (account.ThunderbirdKeys.Count > 0)
                        {
                            streamWriter.WriteComment("Thunderbird keys for accounts configured through the OpaqueMail Proxy settings app.");
                            streamWriter.WriteElementString("ThunderbirdKeyCount", account.ThunderbirdKeys.Count.ToString());

                            int thunderbirdKeyId = 0;
                            foreach (string thunderbirdKey in account.ThunderbirdKeys)
                                streamWriter.WriteElementString("ThunderbirdKey" + (++thunderbirdKeyId).ToString(), thunderbirdKey);
                        }

                        streamWriter.WriteEndElement();
                    }
                }

                streamWriter.WriteEndElement();

                streamWriter.WriteStartElement("IMAP");

                streamWriter.WriteComment("The number of IMAP proxy services to run.  Each proxy's settings will be outlined in subsequent <Service#/> blocks.");
                streamWriter.WriteElementString("ServiceCount", imapServiceCount.ToString());

                int imapServiceId = 0;
                foreach (ProxyAccount account in accounts)
                {
                    if (!string.IsNullOrEmpty(account.RemoteImapServer) && account.Matched)
                    {
                        streamWriter.WriteStartElement("Service" + (++imapServiceId).ToString());

                        streamWriter.WriteComment("IP addresses to accept connections from.  Delete or set value to \"*\" to accept connections from any IP.");
                        streamWriter.WriteComment("Individual IPs can be specified, separated by commas, or ranges can be specified.  The \"*\" wildcard character is supported.");
                        streamWriter.WriteComment("By default, connections are only accepted from the localhost.");
                        streamWriter.WriteElementString("AcceptedIPs", account.ImapAcceptedIPs ?? defaultAcceptedIPs);

                        streamWriter.WriteComment("Local IP address to listen on.  \"Any\" means listen on all IPs.");
                        streamWriter.WriteElementString("LocalIPAddress", account.LocalImapIPAddress ?? "Any");
                        streamWriter.WriteComment("Local port to listen on.");
                        streamWriter.WriteElementString("LocalPort", account.LocalImapPort > 0 ? account.LocalImapPort.ToString() : "993");
                        streamWriter.WriteComment("Whether local connections support TLS/SSL protection.");
                        streamWriter.WriteElementString("LocalEnableSSL", account.LocalImapEnableSsl.ToString());

                        streamWriter.WriteComment("Remote IMAP server hostname to connect to.  Common values: imap.gmail.com, imap.mail.yahoo.com");
                        streamWriter.WriteElementString("RemoteServerHostName", account.RemoteImapServer ?? "Any");
                        streamWriter.WriteComment("Remote IMAP server port to connect to.  993 is recommended, but 143 may be required.");
                        streamWriter.WriteElementString("RemoteServerPort", account.RemoteImapPort > 0 ? account.RemoteImapPort.ToString() : "993");
                        streamWriter.WriteComment("Whether the remote IMAP server supports TLS/SSL protection.");
                        streamWriter.WriteElementString("RemoteServerEnableSSL", account.RemoteImapEnableSsl.ToString());

                        streamWriter.WriteComment("Where log files should be stored, if any.  Leave blank to avoid logging.");
                        streamWriter.WriteComment("Date and instance variables can be encased in angle braces.  For example, \"Logs\\IMAPProxy{#}-{yyyy-MM-dd}.log\".");
                        streamWriter.WriteElementString("LogFile", account.ImapLogFile ?? "Logs\\IMAPProxy{#}-{yyyy-MM-dd}.log");

                        streamWriter.WriteComment("Proxy logging level, determining how much information is logged.  Possible values: None, Critical, Error, Warning, Information, Verbose, Raw");
                        streamWriter.WriteElementString("LogLevel", account.ImapLogLevel.ToString());

                        if (account.OutlookRegistryKeys.Count > 0)
                        {
                            streamWriter.WriteComment("Outlook registry keys for accounts configured through the OpaqueMail Proxy settings app.");
                            streamWriter.WriteElementString("OutlookRegistryKeyCount", account.OutlookRegistryKeys.Count.ToString());

                            int registryKeyId = 0;
                            foreach (string registryKey in account.OutlookRegistryKeys)
                                streamWriter.WriteElementString("OutlookRegistryKey" + (++registryKeyId).ToString(), registryKey);
                        }

                        if (account.ThunderbirdKeys.Count > 0)
                        {
                            streamWriter.WriteComment("Thunderbird keys for accounts configured through the OpaqueMail Proxy settings app.");
                            streamWriter.WriteElementString("ThunderbirdKeyCount", account.ThunderbirdKeys.Count.ToString());

                            int thunderbirdKeyId = 0;
                            foreach (string thunderbirdKey in account.ThunderbirdKeys)
                                streamWriter.WriteElementString("ThunderbirdKey" + (++thunderbirdKeyId).ToString(), thunderbirdKey);
                        }

                        streamWriter.WriteEndElement();
                    }
                }

                streamWriter.WriteEndElement();

                streamWriter.WriteStartElement("POP3");

                streamWriter.WriteComment("The number of POP3 proxy services to run.  Each proxy's settings will be outlined in subsequent <Service#/> blocks.");
                streamWriter.WriteElementString("ServiceCount", pop3ServiceCount.ToString());

                int pop3ServiceId = 0;
                foreach (ProxyAccount account in accounts)
                {
                    if (!string.IsNullOrEmpty(account.RemotePop3Server) && account.Matched)
                    {
                        streamWriter.WriteStartElement("Service" + (++pop3ServiceId).ToString());

                        streamWriter.WriteComment("IP addresses to accept connections from.  Delete or set value to \"*\" to accept connections from any IP.");
                        streamWriter.WriteComment("Individual IPs can be specified, separated by commas, or ranges can be specified.  The \"*\" wildcard character is supported.");
                        streamWriter.WriteComment("By default, connections are only accepted from the localhost.");
                        streamWriter.WriteElementString("AcceptedIPs", account.Pop3AcceptedIPs ?? defaultAcceptedIPs);

                        streamWriter.WriteComment("Local IP address to listen on.  \"Any\" means listen on all IPs.");
                        streamWriter.WriteElementString("LocalIPAddress", account.LocalPop3IPAddress ?? "Any");
                        streamWriter.WriteComment("Local port to listen on.");
                        streamWriter.WriteElementString("LocalPort", account.LocalPop3Port > 0 ? account.LocalPop3Port.ToString() : "995");
                        streamWriter.WriteComment("Whether local connections support TLS/SSL protection.");
                        streamWriter.WriteElementString("LocalEnableSSL", account.LocalPop3EnableSsl.ToString());

                        streamWriter.WriteComment("Remote POP3 server hostname to connect to.  Common values: pop.gmail.com, pop3.live.com, pop.mail.yahoo.com");
                        streamWriter.WriteElementString("RemoteServerHostName", account.RemotePop3Server ?? "Any");
                        streamWriter.WriteComment("Remote POP3 server port to connect to.  995 is recommended, but 110 may be required.");
                        streamWriter.WriteElementString("RemoteServerPort", account.RemotePop3Port > 0 ? account.RemotePop3Port.ToString() : "995");
                        streamWriter.WriteComment("Whether the remote POP3 server supports TLS/SSL protection.");
                        streamWriter.WriteElementString("RemoteServerEnableSSL", account.RemotePop3EnableSsl.ToString());

                        streamWriter.WriteComment("Where log files should be stored, if any.  Leave blank to avoid logging.");
                        streamWriter.WriteComment("Date and instance variables can be encased in angle braces.  For example, \"Logs\\POP3Proxy{#}-{yyyy-MM-dd}.log\".");
                        streamWriter.WriteElementString("LogFile", account.Pop3LogFile ?? "Logs\\POP3Proxy{#}-{yyyy-MM-dd}.log");

                        streamWriter.WriteComment("Proxy logging level, determining how much information is logged.  Possible values: None, Critical, Error, Warning, Information, Verbose, Raw");
                        streamWriter.WriteElementString("LogLevel", account.Pop3LogLevel.ToString());

                        if (account.OutlookRegistryKeys.Count > 0)
                        {
                            streamWriter.WriteComment("Outlook registry keys for accounts configured through the OpaqueMail Proxy settings app.");
                            streamWriter.WriteElementString("OutlookRegistryKeyCount", account.OutlookRegistryKeys.Count.ToString());

                            int registryKeyId = 0;
                            foreach (string registryKey in account.OutlookRegistryKeys)
                                streamWriter.WriteElementString("OutlookRegistryKey" + (++registryKeyId).ToString(), registryKey);
                        }

                        if (account.ThunderbirdKeys.Count > 0)
                        {
                            streamWriter.WriteComment("Thunderbird keys for accounts configured through the OpaqueMail Proxy settings app.");
                            streamWriter.WriteElementString("ThunderbirdKeyCount", account.ThunderbirdKeys.Count.ToString());

                            int thunderbirdKeyId = 0;
                            foreach (string thunderbirdKey in account.ThunderbirdKeys)
                                streamWriter.WriteElementString("ThunderbirdKey" + (++thunderbirdKeyId).ToString(), thunderbirdKey);
                        }

                        streamWriter.WriteEndElement();
                    }
                }

                streamWriter.WriteEndElement();

                streamWriter.WriteEndElement();
            }

            // Sixth, address loopback firewall settings.
            if (UpdateFirewall.Checked)
            {
                // Enable the back connection hostname to avoid loopback checks.
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0", true))
                {
                    object loopbackObject = key.GetValue("BackConnectionHostNames");
                    if (loopbackObject != null)
                    {
                        string[] loopbackValue = (string[])loopbackObject;

                        bool loopbackFound = false;
                        string[] newLoopbackValue = new string[loopbackValue.Length + 1];
                        for (int i = 0; i < loopbackValue.Length; i++)
                        {
                            if (loopbackValue[i].ToUpper() == fqdn.ToUpper())
                            {
                                loopbackFound = true;
                                break;
                            }
                            newLoopbackValue[i] = loopbackValue[i];
                        }

                        if (!loopbackFound)
                        {
                            newLoopbackValue[newLoopbackValue.Length - 1] = Functions.GetLocalFQDN();
                            key.SetValue("BackConnectionHostNames", newLoopbackValue);

                            StopService("IISAdmin");
                            StartService("IISAdmin");
                        }
                    }
                    else
                    {
                        key.SetValue("BackConnectionHostNames", new string[] { fqdn });

                        StopService("IISAdmin");
                        StartService("IISAdmin");
                    }
                }

                // Open up Windows 8 Mail loopback.
                try
                {
                    Windows8MailHelper windows8MailLoopbackHelper = new Windows8MailHelper();
                    windows8MailLoopbackHelper.EnableWindows8MailLoopback();
                }
                catch { }
            }

            // Seventh, restart the OpaqueMail service.
            InstallService();
            StartService();

            // Eighth, rewrite the Outlook registry values.
            foreach (string outlookVersion in OutlookVersions.Keys)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + outlookVersion + @"\Outlook\Profiles\Outlook\9375CFF0413111d3B88A00104B2A6676", false))
                {
                    if (key != null)
                    {
                        string[] subkeyNames = key.GetSubKeyNames();
                        if (subkeyNames != null)
                        {
                            foreach (string subkeyName in subkeyNames)
                            {
                                using (RegistryKey subKey = key.OpenSubKey(subkeyName, true))
                                {
                                    string smtpServer = GetOutlookRegistryValue(subKey, "SMTP Server");
                                    if (!string.IsNullOrEmpty(smtpServer))
                                    {
                                        foreach (ProxyAccount account in accounts)
                                        {
                                            // If matched, set to use the local proxy.  If not matched and we previously used the local proxy, switch back to the original value.
                                            if (account.OutlookRegistryKeys.Contains(subKey.Name))
                                            {
                                                if (account.Matched)
                                                {
                                                    subKey.SetValue("SMTP Server", Encoding.Unicode.GetBytes(fqdn + "\0"));
                                                    subKey.SetValue("SMTP Port", account.LocalSmtpPort);
                                                    subKey.SetValue("SMTP Use SSL", account.LocalSmtpEnableSsl ? 1 : 0);
                                                }
                                                else
                                                {
                                                    subKey.SetValue("SMTP Server", Encoding.Unicode.GetBytes(account.RemoteSmtpServer));
                                                    subKey.SetValue("SMTP Port", account.RemoteSmtpPort);
                                                    subKey.SetValue("SMTP Use SSL", account.RemoteSmtpEnableSsl ? 1 : 0);
                                                }
                                            }
                                        }
                                    }

                                    string imapServer = GetOutlookRegistryValue(subKey, "IMAP Server");
                                    if (!string.IsNullOrEmpty(imapServer))
                                    {
                                        foreach (ProxyAccount account in accounts)
                                        {
                                            // If matched, set to use the local proxy.  If not matched and we previously used the local proxy, switch back to the original value.
                                            if (account.OutlookRegistryKeys.Contains(subKey.Name))
                                            {
                                                if (account.Matched)
                                                {
                                                    subKey.SetValue("IMAP Server", Encoding.Unicode.GetBytes(fqdn + "\0"));
                                                    subKey.SetValue("IMAP Port", account.LocalImapPort);
                                                    subKey.SetValue("IMAP Use SSL", account.LocalImapEnableSsl ? 1 : 0);
                                                }
                                                else
                                                {
                                                    subKey.SetValue("IMAP Server", Encoding.Unicode.GetBytes(account.RemoteImapServer + "\0"));
                                                    subKey.SetValue("IMAP Port", account.RemoteImapPort);
                                                    subKey.SetValue("IMAP Use SSL", account.RemoteImapEnableSsl ? 1 : 0);
                                                }
                                            }
                                        }
                                    }

                                    string pop3Server = GetOutlookRegistryValue(subKey, "POP3 Server");
                                    if (!string.IsNullOrEmpty(pop3Server))
                                    {
                                        foreach (ProxyAccount account in accounts)
                                        {
                                            // If matched, set to use the local proxy.  If not matched and we previously used the local proxy, switch back to the original value.
                                            if (account.OutlookRegistryKeys.Contains(subKey.Name))
                                            {
                                                if (account.Matched)
                                                {
                                                    subKey.SetValue("POP3 Server", Encoding.Unicode.GetBytes(fqdn + "\0"));
                                                    subKey.SetValue("POP3 Port", account.LocalPop3Port);
                                                    subKey.SetValue("POP3 Use SSL", account.LocalPop3EnableSsl ? 1 : 0);
                                                }
                                                else
                                                {
                                                    subKey.SetValue("POP3 Server", Encoding.Unicode.GetBytes(account.RemotePop3Server + "\0"));
                                                    subKey.SetValue("POP3 Port", account.RemotePop3Port);
                                                    subKey.SetValue("POP3 Use SSL", account.RemotePop3EnableSsl ? 1 : 0);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Ninth, rewrite the Thunderbird registry values.
            if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Thunderbird\\Profiles"))
            {
                foreach (string directory in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Thunderbird\\Profiles"))
                {
                    if (File.Exists(directory + "\\prefs.js"))
                    {
                        string prefsFile = File.ReadAllText(directory + "\\prefs.js");

                        int keyCount;
                        int.TryParse(Functions.ReturnBetween(prefsFile, "user_pref(\"mail.account.lastKey\", ", ")"), out keyCount);
                        for (int i = 1; i <= keyCount; i++)
                        {
                            string thunderbirdKey = directory + "~" + i.ToString();

                            foreach (ProxyAccount account in accounts)
                            {
                                // If matched, set to use the local proxy.  If not matched and we previously used the local proxy, switch back to the original value.
                                if (account.ThunderbirdKeys.Contains(thunderbirdKey))
                                {
                                    if (account.Matched)
                                    {
                                        if (Functions.ReturnBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".type\", \"", "\"").ToLower() == "pop3")
                                        {
                                            prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".hostname\", \"", "\"", fqdn);
                                            prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".port\", ", ")", account.LocalPop3Port.ToString());
                                        }
                                        else
                                        {
                                            prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".hostname\", \"", "\"", fqdn);
                                            prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".port\", ", ")", account.LocalImapPort.ToString());
                                        }

                                        prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.smtpserver.smtp" + i.ToString() + ".hostname\", \"", "\"", fqdn);
                                        prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.smtpserver.smtp" + i.ToString() + ".port\", ", ")", account.LocalSmtpPort.ToString());
                                        prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".is_gmail\", ", ")", "false");
                                    }
                                    else
                                    {
                                        if (Functions.ReturnBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".type\", \"", "\"").ToLower() == "pop3")
                                        {
                                            prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".hostname\", \"", "\"", account.RemotePop3Server);
                                            prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".port\", ", ")", account.RemotePop3Port.ToString());
                                        }
                                        else
                                        {
                                            prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".hostname\", \"", "\"", account.RemoteImapServer);
                                            prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".port\", ", ")", account.RemoteImapPort.ToString());
                                        }

                                        prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.smtpserver.smtp" + i.ToString() + ".hostname\", \"", "\"", account.RemoteSmtpServer);
                                        prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.smtpserver.smtp" + i.ToString() + ".port\", ", ")", account.RemoteSmtpPort.ToString());

                                        bool isGmail = account.RemoteSmtpServer.ToUpper() == "SMTP.GMAIL.COM" || account.RemoteSmtpServer.ToUpper() == "SMTP.GOOGLEMAIL.COM";
                                        prefsFile = Functions.ReplaceBetween(prefsFile, "user_pref(\"mail.server.server" + i.ToString() + ".is_gmail\", ", ")", isGmail ? "true" : "false");
                                    }
                                }
                            }
                        }

                        // Write the settings file back.
                        File.WriteAllBytes(directory + "\\prefs.js", Encoding.UTF8.GetBytes(prefsFile));
                    }
                }
            }

            // Tenth, save Outlook signatures.
            if (UpdateOutlookSignature.Checked)
            {
                List<string> finalOutlookKeyLocations = new List<string>();
                foreach (ProxyAccount account in accounts)
                {
                    if (account.Matched)
                    {
                        foreach (string keyLocation in account.OutlookRegistryKeys)
                            finalOutlookKeyLocations.Add(keyLocation);
                    }
                }
                SetOutlookSignatures(finalOutlookKeyLocations);

            }
            // Finally, prompt to restart Outlook or Thunderbird.
            Process[] processes = Process.GetProcessesByName("OUTLOOK");
            if (processes.Length > 0)
            {
                DialogResult dr = MessageBox.Show("Outlook is currently running and will need to be restarted before these changes will take effect.  Would you like to restart Outlook now?", "Restart Outlook?", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (dr == System.Windows.Forms.DialogResult.OK)
                {
                    // Stop Outlook.
                    foreach (Process process in processes)
                        process.Kill();

                    // Try to start Outlook.
                    for (int i = 15; i >= 8; i--)
                    {
                        if (File.Exists("C:\\Program Files (x86)\\Microsoft Office\\Office" + i.ToString() + "\\Outlook.exe"))
                        {
                            Process.Start("C:\\Program Files (x86)\\Microsoft Office\\Office" + i.ToString() + "\\Outlook.exe");
                            break;
                        }
                        else if (File.Exists("C:\\Program Files\\Microsoft Office\\Office" + i.ToString() + "\\Outlook.exe"))
                        {
                            Process.Start("C:\\Program Files\\Microsoft Office\\Office" + i.ToString() + "\\Outlook.exe");
                            break;
                        }
                    }
                }
            }
            processes = Process.GetProcessesByName("THUNDERBIRD");
            if (processes.Length > 0)
            {
                DialogResult dr = MessageBox.Show("Thunderbird is currently running and will need to be restarted before these changes will take effect.  Would you like to restart Thunderbird now?", "Restart Thunderbird?", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (dr == System.Windows.Forms.DialogResult.OK)
                {
                    // Stop Thunderbird.
                    foreach (Process process in processes)
                        process.Kill();

                    // Try to start Thunderbird.
                    if (File.Exists("C:\\Program Files (x86)\\Mozilla Thunderbird\\Thunderbird.exe"))
                        Process.Start("C:\\Program Files (x86)\\Mozilla Thunderbird\\Thunderbird.exe");
                }
            }

            UpdateServiceStatus(null);

            MessageBox.Show("OpaqueMail Proxy has been successfully configured and the Windows Service is now running.\r\n\r\nYou may close this program and the proxy will continue to run in the background.", "Success.", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}