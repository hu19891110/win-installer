﻿using InstallAgent;
using PInvoke;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;

namespace XSToolsInstallation
{
    static class Helpers
    {
        public static void Reboot()
        {
            Trace.WriteLine("OK - shutting down");
            AcquireSystemPrivilege(AdvApi32.SE_SHUTDOWN_NAME);

            if (WinVersion.GetVersionValue() >= 0x500 &&
                WinVersion.GetVersionValue() < 0x600)
            {
                User32.ExitWindowsEx(
                    User32.ExitFlags.EWX_REBOOT |
                    User32.ExitFlags.EWX_FORCE,
                    0
                );
            }
            else
            {
                AdvApi32.InitiateSystemShutdownEx(
                    "", "", 0, true, true,
                    AdvApi32.ShtdnReason.MAJOR_OTHER |
                    AdvApi32.ShtdnReason.MINOR_ENVIRONMENT |
                    AdvApi32.ShtdnReason.FLAG_PLANNED
                );
            }
        }

        public static void AcquireSystemPrivilege(string name)
        {
            AdvApi32.TOKEN_PRIVILEGES tkp;
            IntPtr token;

            tkp.Privileges = new AdvApi32.LUID_AND_ATTRIBUTES[1];
            AdvApi32.LookupPrivilegeValue(
                IntPtr.Zero,
                name,
                out tkp.Privileges[0].Luid
            );

            tkp.PrivilegeCount = 1;

            tkp.Privileges[0].Attributes = (uint)AdvApi32.Se_Privilege.ENABLED;

            if (!AdvApi32.OpenProcessToken(
                    Process.GetCurrentProcess().Handle,
                    (uint)(AdvApi32.Token.ADJUST_PRIVILEGES |
                           AdvApi32.Token.QUERY),
                    out token))
            {
                throw new Exception("OpenProcessToken");
            }

            if (!AdvApi32.AdjustTokenPrivileges(
                    token,
                    false,
                    ref tkp,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Exception("AdjustTokenPrivileges"); ;
            }
        }

        public static void InstallCertificates(string certDir)
        {
            string[] certificateNames = {
                "eapcitrix.cer",
                "eapcodesign.cer",
                "eaproot.cer"
            };

            foreach (string certName in certificateNames)
            {
                string fullCertPath = Path.Combine(
                    certDir, certName
                );

                if (!File.Exists(fullCertPath))
                {
                    Trace.WriteLine(
                        String.Format("\'{0}\' does not exist", fullCertPath)
                    );
                    continue;
                }

                X509Store store = new X509Store(
                    StoreName.TrustedPublisher,
                    StoreLocation.LocalMachine
                );

                X509Certificate2 cert =
                    new X509Certificate2(fullCertPath);

                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
                store.Close();
            }
        }

        public static int BitIdxFromFlag(uint flag)
        {
            if (flag == 0)
            {
                throw new Exception("\'flag\' is empty");
            }
            else if ((flag & (flag - 1)) != 0)
            // If this is true, 'flag' is not a power of 2,
            // and hence, has more than one bit set
            {
                throw new Exception("\'flag\' has more than one bits set");
            }

            return (int)Math.Log((double)flag, 2.0);
        }

        public static bool ChangeServiceStartMode(
            string serviceName,
            ServiceStartMode mode)
        {
            Trace.WriteLine(
                "Changing Start Mode of service: \'" + serviceName + "\'"
            );

            IntPtr scManagerHandle = AdvApi32.OpenSCManager(
                null,
                null,
                AdvApi32.SC_MANAGER_ALL_ACCESS
            );

            if (scManagerHandle == IntPtr.Zero)
            {
                Trace.WriteLine("Open Service Manager Error");
                return false;
            }

            IntPtr serviceHandle = AdvApi32.OpenService(
                scManagerHandle,
                serviceName,
                AdvApi32.SERVICE_QUERY_CONFIG |
                    AdvApi32.SERVICE_CHANGE_CONFIG
            );

            if (serviceHandle == IntPtr.Zero)
            {
                Trace.WriteLine("Open Service Error");
                return false;
            }

            if (!AdvApi32.ChangeServiceConfig(
                    serviceHandle,
                    AdvApi32.SERVICE_NO_CHANGE,
                    (uint)mode,
                    AdvApi32.SERVICE_NO_CHANGE,
                    null,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    null))
            {
                Trace.WriteLine(
                    "Could not change service's Start Mode: " +
                    new Win32Exception(
                        Marshal.GetLastWin32Error()
                    ).Message);
                return false;
            }

            AdvApi32.CloseServiceHandle(serviceHandle);
            AdvApi32.CloseServiceHandle(scManagerHandle);

            Trace.WriteLine(
                "Start Mode successfully changed to: \'" +
                mode.ToString() + "\'"
            );

            return true;
        }
    }
}
