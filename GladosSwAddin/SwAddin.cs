using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GladosSwAddin.Helpers;
using GladosSwAddin.Services;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace GladosSwAddin
{
    [ComVisible(true)]
    [Guid("BF78E037-8884-435D-966F-6EAA619F35B0")]
    [ClassInterface(ClassInterfaceType.None)]
    [SwAddin(
        Title = AddinInfo.DisplayName,
        Description = AddinInfo.Description,
        LoadAtStartup = true
    )]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "SolidWorks never calls Dispose on an add-in; the timer's lifetime is "
            + "ConnectToSW..DisconnectFromSW, which creates and disposes it.")]
    public class SwAddin : ISwAddin
    {
        private ISldWorks _swApp;

        // Events (ActiveDocChangeNotify etc.) live on the SldWorks coclass's default
        // event interface, not on ISldWorks — hence a separate coclass reference next to
        // the ISldWorks one.
        private SldWorks _swEventPtr;

        private int _cookie;
        private ITaskpaneView _taskpaneView;
        private GladosTaskPaneControl _taskpaneControl;

        /// <summary>Fires ~200 ms after FileCloseNotify — see OnFileCloseNotify for why the
        /// check can't run synchronously there. Re-armed (Stop/Start) rather than recreated on
        /// every close so a burst of closes only schedules one pending check. Created in
        /// ConnectToSW and disposed in DisconnectFromSW, the add-in's actual lifetime.</summary>
        private System.Windows.Forms.Timer _fileCloseCheckTimer;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            _swApp = (ISldWorks)ThisSW;
            _swEventPtr = (SldWorks)ThisSW;
            _cookie = Cookie;

            Log.Info(string.Format(
                CultureInfo.InvariantCulture,
                "Add-in {0} connecting to SOLIDWORKS {1}",
                AddinInfo.Version,
                _swApp.RevisionNumber()));

            // Leftovers of earlier Embed Imported Components runs; see ImportedComponentsService.
            ImportedComponentsService.PurgeStaleWorkFolders();

            _fileCloseCheckTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _fileCloseCheckTimer.Tick += OnFileCloseCheckTick;

            CreateTaskpane();

            // ActiveDocChangeNotify alone proved unreliable in practice —
            // ActiveModelDocChangeNotify is what actually fires consistently, so both are
            // hooked to the same handler here.
            _swEventPtr.ActiveDocChangeNotify += OnActiveDocChangeNotify;
            _swEventPtr.ActiveModelDocChangeNotify += OnActiveDocChangeNotify;

            // Neither ActiveDoc*ChangeNotify fires when the last open document is closed
            // (there's nothing to change "to"), so the taskpane would otherwise keep showing
            // the just-closed document's data forever. FileCloseNotify fires for every close;
            // see OnFileCloseNotify for how it's turned into an active-doc re-check. The 1 s
            // watchdog started below (GladosTaskPaneControl.StartDocumentWatchdog) is the
            // backstop for whatever this event-driven pair still misses.
            _swEventPtr.FileCloseNotify += OnFileCloseNotify;

            // Pick up whatever document is already open — the events above only
            // fire on subsequent changes, not for the doc active at load time.
            OnActiveDocChangeNotify();

            return true;
        }

        public bool DisconnectFromSW()
        {
            Log.Info("Add-in disconnecting from SOLIDWORKS");

            if (_swEventPtr != null)
            {
                _swEventPtr.ActiveDocChangeNotify -= OnActiveDocChangeNotify;
                _swEventPtr.ActiveModelDocChangeNotify -= OnActiveDocChangeNotify;
                _swEventPtr.FileCloseNotify -= OnFileCloseNotify;
            }

            if (_fileCloseCheckTimer != null)
            {
                _fileCloseCheckTimer.Stop();
                _fileCloseCheckTimer.Tick -= OnFileCloseCheckTick;
                _fileCloseCheckTimer.Dispose();
                _fileCloseCheckTimer = null;
            }

            if (_taskpaneControl != null)
            {
                _taskpaneControl.StopDocumentWatchdog();
                _taskpaneControl.ActiveDocumentProvider = null;
                _taskpaneControl.SolidWorksProvider = null;
            }

            if (_taskpaneView != null)
            {
                _taskpaneView.DeleteView();
                _taskpaneView = null;
            }

            _taskpaneControl = null;
            _swEventPtr = null;
            _swApp = null;
            return true;
        }

        private int OnActiveDocChangeNotify()
        {
            ModelDoc2 model = null;
            try
            {
                model = (ModelDoc2)_swApp?.ActiveDoc;
            }
            catch (COMException ex)
            {
                Log.Debug("ActiveDoc threw during an active-document change: " + ex.Message);
                // Same reasoning as OnFileCloseCheckTick: a document mid-transition can make
                // ActiveDoc throw, and an exception escaping a SolidWorks event handler is never
                // acceptable — fall back to "nothing active"; the watchdog corrects it later.
            }

            _taskpaneControl?.OnActiveDocChanged(model);
            return 0;
        }

        /// <summary>Fires while the document being closed is still ISldWorks.ActiveDoc — reading
        /// ActiveDoc synchronously here would just report the very document that's closing.
        /// Neither ActiveDocChangeNotify nor ActiveModelDocChangeNotify fires when the last open
        /// document closes (there's nothing to change "to"), so this notify — which fires for
        /// every close — is the only event-driven signal available for that case.
        /// GetDocumentCount() was tried as a same-tick proxy for "is this the last one", but it
        /// counts every loaded document, including assembly components pulled in without their
        /// own window, so it rarely reaches the count of 1 or fewer this needed and the pane
        /// stayed stale.
        /// Instead, defer the actual check a short time (_fileCloseCheckTimer, ~200 ms) so
        /// SolidWorks has finished the close — by then ActiveDoc is either the next document or
        /// null — and hand whatever it is to OnActiveDocChanged, same as the two notifies above.
        /// GladosTaskPaneControl's 1 s watchdog is the remaining backstop for whatever timing
        /// this still doesn't cover.</summary>
        private int OnFileCloseNotify(string fileName, int reason)
        {
            _fileCloseCheckTimer?.Stop();
            _fileCloseCheckTimer?.Start();
            return 0;
        }

        private void OnFileCloseCheckTick(object sender, EventArgs e)
        {
            _fileCloseCheckTimer?.Stop();

            ModelDoc2 active = null;
            try
            {
                active = (ModelDoc2)_swApp?.ActiveDoc;
            }
            catch (COMException ex)
            {
                Log.Debug("ActiveDoc threw after a file close: " + ex.Message);
                // SolidWorks may still be tearing the document down — treat it as "nothing
                // active" rather than leaving the pane showing the closed document.
            }

            _taskpaneControl?.OnActiveDocChanged(active);
        }

        /// <summary>Builds the taskpane icon from all six sizes SolidWorks expects
        /// (CreateTaskpaneView3's ImageList; see its SolidWorks API doc) rather than the single
        /// path CreateTaskpaneView2 takes — passing it a comma-joined string of three paths (the
        /// old code here) is a mismatch that leaves the taskpane iconless. Order matters: 20, 32,
        /// 40, 64, 96, 128 px, matching the embedded Glados*.bmp resources.</summary>
        private void CreateTaskpane()
        {
            string[] iconPaths =
            {
                IconManager.Extract("GladosSwAddin.Resources.CommandIcons.Glados20.bmp", "Glados20.bmp"),
                IconManager.Extract("GladosSwAddin.Resources.CommandIcons.Glados32.bmp", "Glados32.bmp"),
                IconManager.Extract("GladosSwAddin.Resources.CommandIcons.Glados40.bmp", "Glados40.bmp"),
                IconManager.Extract("GladosSwAddin.Resources.CommandIcons.Glados64.bmp", "Glados64.bmp"),
                IconManager.Extract("GladosSwAddin.Resources.CommandIcons.Glados96.bmp", "Glados96.bmp"),
                IconManager.Extract("GladosSwAddin.Resources.CommandIcons.Glados128.bmp", "Glados128.bmp"),
            };

            _taskpaneView = _swApp.CreateTaskpaneView3(iconPaths, AddinInfo.DisplayName);
            _taskpaneControl = (GladosTaskPaneControl)
                _taskpaneView.AddControl("GladosSwAddin.GladosTaskPaneControl", string.Empty);

            // The control needs the live active document to run its watchdog (see
            // GladosTaskPaneControl.ActiveDocumentProvider) without owning ISldWorks itself.
            _taskpaneControl.ActiveDocumentProvider = () => (ModelDoc2)_swApp.ActiveDoc;
            _taskpaneControl.SolidWorksProvider = () => _swApp;
            _taskpaneControl.StartDocumentWatchdog();
        }

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            string guid = "{" + t.GUID.ToString().ToUpperInvariant() + "}";

            try
            {
                using (
                    RegistryKey key = RegistryKey
                        .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        .CreateSubKey(@"SOFTWARE\SOLIDWORKS\AddIns\" + guid)
                )
                {
                    key.SetValue(null, 0);
                    key.SetValue("Title", AddinInfo.DisplayName);
                    key.SetValue("Description", AddinInfo.Description);
                    // The Add-Ins dialog's icon: a 16x16 PNG the build copies next to the DLL
                    // (the installer registers the same value for its install folder).
                    key.SetValue("Icon Path", Path.Combine(
                        Path.GetDirectoryName(t.Assembly.Location) ?? string.Empty, "GladosSwAddin.png"));
                }

                using (
                    RegistryKey key = RegistryKey
                        .OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64)
                        .CreateSubKey(@"SOFTWARE\SOLIDWORKS\AddInsStartup\" + guid)
                )
                {
                    key.SetValue(null, 1);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to register " + AddinInfo.DisplayName, ex);
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            string guid = "{" + t.GUID.ToString().ToUpperInvariant() + "}";

            try
            {
                RegistryKey
                    .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .DeleteSubKeyTree(@"SOFTWARE\SOLIDWORKS\AddIns\" + guid, false);

                RegistryKey
                    .OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64)
                    .DeleteSubKeyTree(@"SOFTWARE\SOLIDWORKS\AddInsStartup\" + guid, false);
            }
            catch
            {
                // Ignore cleanup errors during uninstall
            }
        }
    }
}
