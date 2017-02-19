﻿using System;
using NetFwTypeLib;
using System.Linq;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Media;

namespace Wokhan.WindowsFirewallNotifier.Common.Helpers
{
    public class FirewallHelper
    {
        //[DllImport("user32.dll", SetLastError = true)]
        //private static extern int LoadString(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //private static extern IntPtr FindResource(IntPtr hModule, int lpName, int lpType);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //private static extern IntPtr LoadLibraryEx(string lpFileName, [In] IntPtr hFile, uint dwFlags);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //private static extern bool FreeLibrary([In] IntPtr hModule);

        //private const uint LOAD_LIBRARY_AS_DATAFILE = 0&00000002;

        private static INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
        private const string indParamFormat = "{0}#$#{1}#$#{2}#$#{3}#$#{4}#$#{5}#$#{6}#$#{7}#$#{8}";
        private static string WFNRuleManagerEXE = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuleManager.exe");

        public abstract class Rule
        {
            public abstract NET_FW_ACTION_ Action { get; }
            public abstract bool Active { get; }
            public abstract string ApplicationName { get; }
            public abstract string Description { get; }
            public abstract NET_FW_RULE_DIRECTION_ Direction { get; }
            public abstract bool EdgeTraversal { get; }
            public abstract bool Enabled { get; }
            public abstract string Grouping { get; }
            public abstract string IcmpTypesAndCodes { get; }
            public abstract object Interfaces { get; }
            public abstract string InterfaceTypes { get; }
            public abstract string LocalAddresses { get; }
            public abstract string LocalPorts { get; }
            public abstract string Name { get; }
            public abstract int Profiles { get; }
            public abstract int Protocol { get; }
            public abstract string RemoteAddresses { get; }
            public abstract string RemotePorts { get; }
            public abstract string serviceName { get; }

            //FIXME: v2.10?
            public abstract int EdgeTraversalOptions { get; }

            // For metro apps (v2.20)
            //public abstract string AppPkgId { get; } //FIXME: !!!
            //public abstract string Security { get; }
            //public abstract string LUOwn { get; }
            //public abstract string LUAuth { get; }
            //public abstract string EmbedCtxt { get; }

            //FIXME: Need to parse: (RA42=) RmtIntrAnet

            private ImageSource _icon = null;
            public ImageSource Icon
            {
                get
                {
                    if (_icon == null)
                    {
                        _icon = IconHelper.GetIcon(this.ApplicationName); //FIXME: This is now expanded... Is that a problem?!?
                    }

                    return _icon;
                }
            }

            public string ProfilesStr { get { return getProfile(this.Profiles); } }

            public string ActionStr { get { return getAction(this.Action); } }

            public string DirectionStr { get { return getDirection(this.Direction); } }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="direction"></param>
            /// <returns></returns>
            private static string getDirection(NET_FW_RULE_DIRECTION_ direction)
            {
                switch (direction)
                {
                    case NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN:
                        return Resources.FW_DIR_IN;

                    case NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT:
                        return Resources.FW_DIR_OUT;

                    case NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_MAX:
                        return Resources.FW_DIR_BOTH;

                    default:
                        LogHelper.Warning("Unknown direction type: " + direction.ToString());
                        return "?";
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="action"></param>
            /// <returns></returns>
            private static string getAction(NET_FW_ACTION_ action)
            {
                switch (action)
                {
                    case NET_FW_ACTION_.NET_FW_ACTION_ALLOW:
                        return Resources.FW_RULE_ALLOW;

                    case NET_FW_ACTION_.NET_FW_ACTION_BLOCK:
                        return Resources.FW_RULE_BLOCK;

                    default:
                        LogHelper.Warning("Unknown action type: " + action.ToString());
                        return "?";
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="profile_type"></param>
            /// <returns></returns>
            internal static string getProfile(int profile_type)
            {
                return FirewallHelper.GetProfileAsText(profile_type);
            }
        }

        public class WSHRule : Rule
        {
            private ILookup<string, string> parsed;

            private Version version;

            public WSHRule(string regRule)
            {
                var parts = regRule.TrimEnd('|').Split('|');
                if (!(parts[0].StartsWith("v") || parts[0].StartsWith("V")))
                {
                    throw new Exception("Unknown rule versioning scheme: " + parts[0]);
                }
                this.version = new Version(parts[0].Substring(1));
                parsed = parts.Skip(1).Select(s => s.Split('=')).ToLookup(s => s[0].ToLower(), s => s[1]);
            }

            public override NET_FW_ACTION_ Action
            {
                get
                {
                    return parsed["action"].FirstOrDefault() == "Block" ? NET_FW_ACTION_.NET_FW_ACTION_BLOCK : NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                }
            }

            public override bool Active //FIXME: Start using this!
            {
                get
                {
                    if (parsed["AppPkgId"].FirstOrDefault() != "")
                        return false;
                    return parsed["active"].FirstOrDefault() == "TRUE";
                }
            }

            public override string ApplicationName
            {
                get
                {
                    return parsed["app"].FirstOrDefault();
                }
            }

            public override string Description
            {
                get
                {
                    return parsed["desc"].FirstOrDefault();
                }
            }

            public override NET_FW_RULE_DIRECTION_ Direction
            {
                get
                {
                    return parsed["dir"].FirstOrDefault() == "In" ? NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN : NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
                }
            }

            public override bool EdgeTraversal
            {
                get
                {
                    return true; //FIXME: !
                }
            }

            public override int EdgeTraversalOptions
            {
                get
                {
                    return 0; //FIXME: !
                }
            }

            public override bool Enabled
            {
                get
                {
                    return parsed.Contains("active") ? bool.Parse(parsed["active"].FirstOrDefault()) : true;
                }
            }

            public override string Grouping
            {
                get
                {
                    return ""; //FIXME: !
                }
            }

            public override string IcmpTypesAndCodes
            {
                get
                {
                    return ""; //FIXME: !
                }
            }

            public override object Interfaces
            {
                get
                {
                    return null; //FIXME: !
                }
            }

            public override string InterfaceTypes
            {
                get
                {
                    return ""; //FIXME: !
                }
            }

            public override string LocalAddresses
            {
                get
                {
                    return String.Join(", ", parsed["la4"].Concat(parsed["la6"]).ToArray());
                }
            }

            public override string LocalPorts
            {
                get
                {
                    return parsed["lport"].FirstOrDefault();
                }
            }

            public override string Name
            {
                get
                {
                    return "WSH - " + CommonHelper.GetMSResourceString(parsed["name"].FirstOrDefault());
                }
            }

            public override int Profiles
            {
                get
                {
                    if (parsed["profile"].Count() == 0)
                    {
                        return (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL;
                    }
                    int profiles = 0;
                    foreach (var profile in parsed["profile"])
                    {
                        switch (profile)
                        {
                            case "Public":
                                profiles += (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC;
                                break;

                            case "Domain":
                                profiles += (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN;
                                break;

                            case "Private":
                                profiles += (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE;
                                break;

                            default:
                                LogHelper.Warning("Unknown profile type: " + profile);
                                break;
                        }
                    }
                    return profiles;
                }
            }

            public override int Protocol
            {
                get
                {
                    if (parsed.Contains("protocol") && parsed["protocol"].Any())
                    {
                        return int.Parse(parsed["protocol"].First());
                    }
                    else
                    {
                        return (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY;
                    }
                }
            }

            public override string RemoteAddresses
            {
                get
                {
                    return String.Join(", ", parsed["ra4"].Concat(parsed["ra6"]).ToArray());
                }
            }

            public override string RemotePorts
            {
                get
                {
                    return parsed["rport"].FirstOrDefault();
                }
            }

            public override string serviceName
            {
                get
                {
                    return parsed["svc"].FirstOrDefault();
                }
            }

            //FIXME: v2.10?
            //public int EdgeTraversalOptions { get; set; }

            //// Win 8 ?
            //if (this.version >= new Version(2, 20))
            //{
            //    this.AppPkgId = parsed["apppkdid"].FirstOrDefault();
            //    this.Security = parsed["security"].FirstOrDefault();
            //    this.LUAuth = parsed["luauth"].FirstOrDefault();
            //    this.LUOwn = parsed["luown"].FirstOrDefault();
            //}

            //private string resolveString(string p)
            //{
            //    if (p != null && p.StartsWith("@"))
            //    {
            //        string[] res = p.Substring(1).Split(',');

            //        IntPtr lib = LoadLibraryEx(res[0], IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
            //        try
            //        {
            //            //IntPtr strh = FindResource(lib, int.Parse(res[1]), 6);
            //            //if (strh != IntPtr.Zero)
            //            {
            //                StringBuilder sb = new StringBuilder(255); //FIXME: Hardcoded string size!
            //                LoadString(lib, (UInt16)int.Parse(res[1]), sb, sb.Capacity);

            //                return (sb.Length > 0 ? sb.ToString() : p);
            //            }
            //            //else
            //            {
            //                return p;
            //            }
            //        }
            //        finally
            //        {
            //            FreeLibrary(lib);
            //        }
            //    }
            //    else
            //    {
            //        return p;
            //    }
            //}
        }

        public class FwRule : Rule
        {
            private NetFwTypeLib.INetFwRule InnerRule;

            public FwRule(INetFwRule innerRule)
            {
                InnerRule = innerRule;
            }

            public override NET_FW_ACTION_ Action { get { return InnerRule.Action; } }

            public override bool Active { get { return true; } }

            private string _applicationName = null;
            public override string ApplicationName
            {
                get
                {
                    if (_applicationName == null)
                    {
                        _applicationName = (InnerRule.ApplicationName != null ? Environment.ExpandEnvironmentVariables(InnerRule.ApplicationName) : String.Empty);

                    }
                    return _applicationName;
                }
            }

            private string _description = null;
            public override string Description
            {
                get
                {
                    if (_description == null)
                    {
                        _description = CommonHelper.GetMSResourceString(InnerRule.Description);
                    }

                    return _description;
                }
            }

            public override NET_FW_RULE_DIRECTION_ Direction { get { return InnerRule.Direction; } }
            public override bool EdgeTraversal { get { return InnerRule.EdgeTraversal; } }
            public override int EdgeTraversalOptions
            {
                get
                {
                    if (InnerRule is INetFwRule2)
                    {
                        return ((INetFwRule2)InnerRule).EdgeTraversalOptions;
                    }
                    else
                    {
                        return 0; //FIXME: https://msdn.microsoft.com/en-us/library/windows/desktop/dd607258(v=vs.85).aspx   Proper default value...?
                    }
                }
            }
            public override bool Enabled { get { return InnerRule.Enabled; } }
            public override string Grouping { get { return InnerRule.Grouping; } }
            public override string IcmpTypesAndCodes { get { return InnerRule.IcmpTypesAndCodes; } }
            public override object Interfaces { get { return InnerRule.Interfaces; } }
            public override string InterfaceTypes { get { return InnerRule.InterfaceTypes; } }
            public override string LocalAddresses { get { return InnerRule.LocalAddresses; } }
            public override string LocalPorts { get { return InnerRule.LocalPorts; } }

            private string _name = null;
            public override string Name
            {
                get
                {
                    if (_name == null)
                    {
                        _name = CommonHelper.GetMSResourceString(InnerRule.Name);
                    }

                    return _name;
                }
            }

            public override int Profiles { get { return InnerRule.Profiles; } }
            public override int Protocol { get { return InnerRule.Protocol; } }
            public override string RemoteAddresses { get { return InnerRule.RemoteAddresses; } }
            public override string RemotePorts { get { return InnerRule.RemotePorts; } }
            public override string serviceName { get { return InnerRule.serviceName; } }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ruleName"></param>
        /// <returns></returns>
        public static bool RemoveRule(string ruleName)
        {
            try
            {
                firewallPolicy.Rules.Remove(ruleName);

                return true;
            }
            catch (Exception e)
            {
                LogHelper.Error("Unable to remove the rule.", e);
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ruleName"></param>
        /// <param name="currentPath"></param>
        /// <param name="service"></param>
        /// <param name="protocol"></param>
        /// <param name="target"></param>
        /// <param name="targetPort"></param>
        /// <param name="localport"></param>
        /// <returns></returns>
        public static bool AddBlockRuleIndirect(string ruleName, string currentPath, string[] services, int protocol, string target, string targetPort, string localport, bool useCurrentProfile)
        {
            string param = Convert.ToBase64String(Encoding.Unicode.GetBytes(String.Format(indParamFormat, ruleName, currentPath, services != null ? String.Join(",", services) : null, protocol, target, targetPort, localport, useCurrentProfile, "B")));
            return ProcessHelper.getProcessFeedback(WFNRuleManagerEXE, param, true, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ruleName"></param>
        /// <param name="currentPath"></param>
        /// <param name="service"></param>
        /// <param name="protocol"></param>
        /// <param name="target"></param>
        /// <param name="targetPort"></param>
        /// <param name="localport"></param>
        /// <returns></returns>
        public static bool AddBlockRule(string ruleName, string currentPath, string service, int protocol, string target, string targetPort, string localport, bool useCurrentProfile)
        {
            return AddRule(ruleName, currentPath, service, protocol, target, targetPort, localport, NET_FW_ACTION_.NET_FW_ACTION_BLOCK, false, useCurrentProfile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ruleName"></param>
        /// <param name="currentPath"></param>
        /// <param name="service"></param>
        /// <param name="protocol"></param>
        /// <param name="target"></param>
        /// <param name="targetPort"></param>
        /// <param name="localport"></param>
        /// <returns></returns>
        public static bool AddAllowRuleIndirect(string ruleName, string currentPath, string[] services, int protocol, string target, string targetPort, string localport, bool useCurrentProfile)
        {
            string param = Convert.ToBase64String(Encoding.Unicode.GetBytes(String.Format(indParamFormat, ruleName, currentPath, services != null ? String.Join(",", services) : null, protocol, target, targetPort, localport, useCurrentProfile, "A")));
            return ProcessHelper.getProcessFeedback(WFNRuleManagerEXE, param, true, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ruleName"></param>
        /// <param name="currentPath"></param>
        /// <param name="service"></param>
        /// <param name="protocol"></param>
        /// <param name="target"></param>
        /// <param name="targetPort"></param>
        /// <param name="localport"></param>
        /// <returns></returns>
        public static bool AddAllowRule(string ruleName, string currentPath, string service, int protocol, string target, string targetPort, string localport, bool useCurrentProfile)
        {
            return AddRule(ruleName, currentPath, service, protocol, target, targetPort, localport, NET_FW_ACTION_.NET_FW_ACTION_ALLOW, false, useCurrentProfile);
        }

        public static bool AddTempRuleIndirect(string ruleName,string currentPath, string[] services, int protocol, string target, string targetPort, string localport, bool useCurrentProfile)
        {
            string param = Convert.ToBase64String(Encoding.Unicode.GetBytes(String.Format(indParamFormat, ruleName, currentPath, services != null ? String.Join(",", services) : null, protocol, target, targetPort, localport, useCurrentProfile, "T")));
            return ProcessHelper.getProcessFeedback(WFNRuleManagerEXE, param, true, true);
        }

        public static bool AddTempRule(string ruleName, string currentPath, string service, int protocol, string target, string targetPort, string localport, bool useCurrentProfile)
        {
            return AddRule(ruleName, currentPath, service, protocol, target, targetPort, localport, NET_FW_ACTION_.NET_FW_ACTION_ALLOW, true, useCurrentProfile);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ruleName"></param>
        /// <param name="currentPath"></param>
        /// <param name="service"></param>
        /// <param name="protocol"></param>
        /// <param name="target"></param>
        /// <param name="targetPort"></param>
        /// <param name="localport"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static bool AddRule(string ruleName, string currentPath, string service, int protocol, string target, string targetPort, string localport, NET_FW_ACTION_ action, bool isTemp, bool currentProfile = true)
        {
            try
            {
                INetFwRule firewallRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
                firewallRule.Action = action;
                firewallRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
                firewallRule.Enabled = true;
                firewallRule.Profiles = currentProfile ? FirewallHelper.GetCurrentProfile() : FirewallHelper.GetGlobalProfile();
                firewallRule.InterfaceTypes = "All";
                firewallRule.Name = ruleName;
                firewallRule.ApplicationName = currentPath;

                if (!String.IsNullOrEmpty(service))
                {
                    firewallRule.serviceName = service;
                }

                if (protocol != -1)
                {
                    firewallRule.Protocol = normalizeProtocol(protocol);
                }

                if (!String.IsNullOrEmpty(localport))
                {
                    firewallRule.LocalPorts = localport;

                    if (!isTemp)
                    {
                        firewallRule.Name += " [L:" + localport + "]";
                    }
                }

                if (!String.IsNullOrEmpty(target))
                {
                    firewallRule.RemoteAddresses = target;

                    if (!isTemp)
                    {
                        firewallRule.Name += " [T:" + target + "]";
                    }
                }

                if (!String.IsNullOrEmpty(targetPort))
                {
                    firewallRule.RemotePorts = targetPort;

                    if (!isTemp)
                    {
                        firewallRule.Name += " [R:" + targetPort + "]";
                    }
                }

                firewallPolicy.Rules.Add(firewallRule);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception e)
            {
                LogHelper.Error("Unable to create the rule", e);
            }

            return false;
        }

        private static int GetGlobalProfile()
        {
            return (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL;
        }

        public static string getType(int type)
        {
            return Enum.GetName(typeof(NET_FW_PROFILE_TYPE2_), type);
        }

        public static string getProtocolAsString(int protocol)
        {
            //These are the IANA protocol numbers.
            switch (protocol)
            {
                case 1:
                    return "ICMP";

                case (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP: //6
                    return "TCP";

                case (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP: //17
                    return "UDP";

                case 47:
                    return "GRE"; //Used by PPTP, for example.

                case 58:
                    return "ICMPv6";

                case 136:
                    return "UDPLite";

                default:
                    LogHelper.Warning("Unknown protocol type: " + protocol.ToString());
                    return "Unknown";
            }
        }

        /// <summary>
        /// Converts the protocol integer to its NET_FW_IP_PROTOCOL_ representation.
        /// </summary>
        /// <returns></returns>
        public static int normalizeProtocol(int protocol)
        {
            try
            {
                return (int)(NET_FW_IP_PROTOCOL_)protocol;
            }
            catch
            {
                return (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY;
            }
        }

        public static bool CheckFirewallEnabled()
        {
            return firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL] ||
                   firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] ||
                   firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] ||
                   firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool EnableWindowsFirewall()
        {
            try
            {
                firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = true;
                firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = true;
                firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = true;

                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;

                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;

                firewallPolicy.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = false;
                firewallPolicy.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = false;
                firewallPolicy.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = false;

                return true;
            }
            catch
            {
                //FIXME: Log the error?
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool RestoreWindowsFirewall()
        {
            try
            {
                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;

                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;

                return true;
            }
            catch
            {
                //FIXME: Log the error?
            }

            return false;
        }

        private static Rule[] wshRulesCache = null;

        public enum Protocols
        {
            TCP = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP,
            UDP = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP,
            ANY = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY
        }

        public static Rule[] GetRules(bool AlsoGetInactive = false)
        {
            if (wshRulesCache == null)
            {
                var keyStatic = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\RestrictedServices\Static\System");
                var keyConfig = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\RestrictedServices\Configurable\System");

                IEnumerable<string> allkeyvalues = Enumerable.Empty<string>();
                if (keyStatic != null)
                {
                    allkeyvalues = allkeyvalues.Concat(keyStatic.GetValueNames().Select(s => (string)keyStatic.GetValue(s)));
                }
                if (keyConfig != null)
                {
                    allkeyvalues = allkeyvalues.Concat(keyConfig.GetValueNames().Select(s => (string)keyConfig.GetValue(s)));
                }

                if (AlsoGetInactive)
                {
                    wshRulesCache = allkeyvalues.Select(s => new WSHRule(s)).ToArray();
                }
                else
                {
                    wshRulesCache = allkeyvalues.Select(s => new WSHRule(s)).Where(r => r.Active).ToArray();
                }

                if (keyStatic != null)
                {
                    keyStatic.Close();
                }
                if (keyConfig != null)
                {
                    keyConfig.Close();
                }
            }

            return firewallPolicy.Rules.Cast<INetFwRule>()
                                       .Select(r => new FwRule(r))
                                       .Concat(wshRulesCache)
                                       .ToArray();
        }

        public static int GetCurrentProfile()
        {
            return firewallPolicy.CurrentProfileTypes;
        }

        public static bool IsIPProtocol(int protocol)
        {
            //Used to check whether this protocol supports ports.
            return (protocol == (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP || protocol == (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP);
        }

        public static IEnumerable<Rule> GetMatchingRules(string path, int protocol, string target, string targetPort, string localPort, IEnumerable<string> svc, bool blockOnly, bool outgoingOnly = true)
        {
            int currentProfile = GetCurrentProfile(); //This call is relatively slow, and calling it many times causes a startup delay. Let's cache it!
            IEnumerable<Rule> ret = GetRules().Where(r => RuleMatches(r, path, svc, protocol, localPort, target, targetPort, currentProfile));
            if (blockOnly)
            {
                ret = ret.Where(r => r.Action == NET_FW_ACTION_.NET_FW_ACTION_BLOCK);
            }
            if (outgoingOnly)
            {
                ret = ret.Where(r => r.Direction == NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT);
            }

            //Note: This fills up the logfile quite quickly...
            /*LogHelper.Debug("GetMatchingRules: Matched the following rule:");
            LogHelper.Debug("ToMatch: " + path + ", " + protocol + ", " + target + ", " + targetPort + ", " + localPort + ", " + String.Join(",", svc) + ", " + blockOnly.ToString());
            foreach (var r in ret)
            {
                LogHelper.Debug("Matched rule: " + r.ApplicationName + ", " + r.Protocol + ", " + r.RemoteAddresses + ", " + r.RemotePorts + ", " + r.LocalPorts + ", " + r.serviceName);
            }*/

            return ret;
        }


        private static bool RuleMatches(Rule r, string path, IEnumerable<string> svc, int protocol, string localport, string target, string remoteport, int currentProfile)
        {
            bool ret = r.Enabled
                       && (((r.Profiles & currentProfile) != 0) || ((r.Profiles & (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL) != 0))
                       && (String.IsNullOrEmpty(r.ApplicationName) || StringComparer.CurrentCultureIgnoreCase.Compare(r.ApplicationName, path) == 0)
                       && (String.IsNullOrEmpty(r.serviceName) || (svc.Any() && (r.serviceName == "*")) || svc.Contains(r.serviceName, StringComparer.CurrentCultureIgnoreCase))
                       && (r.Protocol == (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY || r.Protocol == protocol)
                       && CheckRuleAddresses(r.RemoteAddresses, target)
                       && CheckRulePorts(r.RemotePorts, remoteport)
                       && CheckRulePorts(r.LocalPorts, localport)
                       //&& r.EdgeTraversal == //@
                       //&& r.Interfaces == //@
                       //&& r.LocalAddresses //@
                       ;

            return ret;
        }


        private static bool CheckRuleAddresses(string ruleAddresses, string checkedAddress)
        {
            //FIXME: Parse properly! See: https://technet.microsoft.com/en-us/aa365366
            if (String.IsNullOrEmpty(ruleAddresses) || ruleAddresses == "*")
            {
                return true;
            }
            if (!checkedAddress.Contains('/'))
            {
                checkedAddress += "/255.255.255.255";
            }
            foreach (string token in ruleAddresses.Split(','))
            {
                if (token == checkedAddress)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool CheckRulePorts(string rulePorts, string checkedPort)
        {
            //FIXME: Untested!
            if (String.IsNullOrEmpty(rulePorts) || rulePorts == "*")
            {
                return true;
            }
            foreach (string token in rulePorts.Split(','))
            {
                if (token == checkedPort)
                {
                    return true;
                }
                int checkedPortInt;
                if (checkedPort.Contains('-') && Int32.TryParse(checkedPort, out checkedPortInt))
                {
                    string[] portRange = checkedPort.Split(new Char[] { '-' }, 1);
                    if ((Int32.Parse(portRange[0]) >= checkedPortInt) && (checkedPortInt >= Int32.Parse(portRange[1])))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static string GetCurrentProfileAsText()
        {
            return GetProfileAsText(GetCurrentProfile());
        }

        public static string GetProfileAsText(int profile_type)
        {

            string[] ret = new string[3];
            int i = 0;
            if (profile_type == (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL)
            {
                ret[i] = Resources.FW_PROFILE_ALL;
            }
            else
            {
                if ((profile_type & (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN) != 0)
                {
                    ret[i++] = Resources.FW_PROFILE_DOMAIN;
                }
                if ((profile_type & (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE) != 0)
                {
                    ret[i++] = Resources.FW_PROFILE_PRIVATE;
                }
                if ((profile_type & (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC) != 0)
                {
                    ret[i++] = Resources.FW_PROFILE_PUBLIC;
                }
            }
            return String.Join(", ", ret, 0, i);
        }

        public class FirewallStatusWrapper
        {
            private static Dictionary<bool, string> _actions = new Dictionary<bool, string>{
                { true, "Block" },
                { false, "Allow"}
            };

            public static Dictionary<bool, string> Actions { get { return _actions; } }

            public enum Status
            {
                DISABLED,
                ENABLED_ALLOW,
                ENABLED_BLOCK,
                ENABLED_NOTIFY
            }

            private Status privateInStatus = Status.DISABLED;
            private Status domainInStatus = Status.DISABLED;
            private Status publicInStatus = Status.DISABLED;

            private Status privateOutStatus = Status.DISABLED;
            private Status domainOutStatus = Status.DISABLED;
            private Status publicOutStatus = Status.DISABLED;

            public bool PrivateIsEnabled { get; set; }
            public bool PrivateIsInBlocked { get; set; }
            public bool PrivateIsOutBlocked { get; set; }
            public bool PrivateIsOutAllowed { get; set; }
            public bool PrivateIsInBlockedNotif { get; set; }
            public bool PrivateIsOutBlockedNotif { get; set; }

            public bool PublicIsEnabled { get; set; }
            public bool PublicIsInBlocked { get; set; }
            public bool PublicIsOutBlocked { get; set; }
            public bool PublicIsOutAllowed { get; set; }
            public bool PublicIsInBlockedNotif { get; set; }
            public bool PublicIsOutBlockedNotif { get; set; }

            public bool DomainIsEnabled { get; set; }
            public bool DomainIsInBlocked { get; set; }
            public bool DomainIsOutBlocked { get; set; }
            public bool DomainIsOutAllowed { get; set; }
            public bool DomainIsInBlockedNotif { get; set; }
            public bool DomainIsOutBlockedNotif { get; set; }

            public string CurrentProfile { get { return GetCurrentProfileAsText(); } }

            public FirewallStatusWrapper()
            {
                updateStatus(NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE, ref privateInStatus, ref privateOutStatus);
                updateStatus(NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC, ref publicInStatus, ref publicOutStatus);
                updateStatus(NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN, ref domainInStatus, ref domainOutStatus);

                PrivateIsEnabled = (privateInStatus != Status.DISABLED);
                PrivateIsInBlocked = (privateInStatus == Status.ENABLED_BLOCK);
                PrivateIsInBlockedNotif = (privateInStatus == Status.ENABLED_NOTIFY);
                PrivateIsOutBlocked = (privateOutStatus == Status.ENABLED_BLOCK);
                PrivateIsOutAllowed = !PrivateIsOutBlocked && !PrivateIsOutBlockedNotif;

                PublicIsEnabled = (publicInStatus != Status.DISABLED);
                PublicIsInBlocked = (publicInStatus == Status.ENABLED_BLOCK);
                PublicIsInBlockedNotif = (publicInStatus == Status.ENABLED_NOTIFY);
                PublicIsOutBlocked = (publicOutStatus == Status.ENABLED_BLOCK);
                PublicIsOutAllowed = !PublicIsOutBlocked && !PublicIsOutBlockedNotif;

                DomainIsEnabled = (domainInStatus != Status.DISABLED);
                DomainIsInBlocked = (domainInStatus == Status.ENABLED_BLOCK);
                DomainIsInBlockedNotif = (domainInStatus == Status.ENABLED_NOTIFY);
                DomainIsOutBlocked = (domainOutStatus == Status.ENABLED_BLOCK);
                DomainIsOutAllowed = !DomainIsOutBlocked && !DomainIsOutBlockedNotif;
            }

            private void updateStatus(NET_FW_PROFILE_TYPE2_ profile, ref Status stat, ref Status statOut)
            {
                if (firewallPolicy.FirewallEnabled[profile])
                {
                    if (firewallPolicy.DefaultInboundAction[profile] == NET_FW_ACTION_.NET_FW_ACTION_BLOCK)
                    {
                        if (firewallPolicy.NotificationsDisabled[profile])
                        {
                            stat = Status.ENABLED_BLOCK;
                        }
                        else
                        {
                            stat = Status.ENABLED_NOTIFY;
                        }
                    }
                    else
                    {
                        stat = Status.ENABLED_ALLOW;
                    }

                    if (firewallPolicy.DefaultOutboundAction[profile] == NET_FW_ACTION_.NET_FW_ACTION_BLOCK)
                    {
                        statOut = Status.ENABLED_BLOCK;
                    }
                    else
                    {
                        statOut = Status.ENABLED_ALLOW;
                    }
                }
            }

            public void Save()
            {
                firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = PrivateIsEnabled;
                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = PrivateIsInBlockedNotif || PrivateIsInBlocked ? NET_FW_ACTION_.NET_FW_ACTION_BLOCK : NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = PrivateIsOutBlockedNotif || PrivateIsOutBlocked ? NET_FW_ACTION_.NET_FW_ACTION_BLOCK : NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallPolicy.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = !PrivateIsInBlockedNotif;

                firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = PublicIsEnabled;
                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = PublicIsInBlockedNotif || PublicIsInBlocked ? NET_FW_ACTION_.NET_FW_ACTION_BLOCK : NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = PublicIsOutBlockedNotif || PublicIsOutBlocked ? NET_FW_ACTION_.NET_FW_ACTION_BLOCK : NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallPolicy.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = !PublicIsInBlockedNotif;

                firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = DomainIsEnabled;
                firewallPolicy.DefaultInboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = DomainIsInBlockedNotif || DomainIsInBlocked ? NET_FW_ACTION_.NET_FW_ACTION_BLOCK : NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallPolicy.DefaultOutboundAction[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = DomainIsOutBlockedNotif || DomainIsOutBlocked ? NET_FW_ACTION_.NET_FW_ACTION_BLOCK : NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallPolicy.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = !DomainIsInBlockedNotif;
            }
        }
    }
}