﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BMW.Rheingold.CoreFramework.Contracts.Programming;
using BMW.Rheingold.Programming.Common;
using BMW.Rheingold.Programming.Controller.SecureCoding.Model;
using BMW.Rheingold.Psdz;
using BMW.Rheingold.Psdz.Client;
using BMW.Rheingold.Psdz.Model;
using BMW.Rheingold.Psdz.Model.Ecu;
using BMW.Rheingold.Psdz.Model.SecureCoding;
using BMW.Rheingold.Psdz.Model.SecurityManagement;
using BMW.Rheingold.Psdz.Model.Sfa;
using BMW.Rheingold.Psdz.Model.Svb;
using BMW.Rheingold.Psdz.Model.Swt;
using BMW.Rheingold.Psdz.Model.Tal;
using BMW.Rheingold.Psdz.Model.Tal.TalFilter;
using BMW.Rheingold.Psdz.Model.Tal.TalStatus;
using Newtonsoft.Json;
using PsdzClient.Programming;

namespace PsdzClient
{
    public partial class FormMain : Form
    {
        private const string DealerId = "32395";
        private const string Baureihe = "G31";
        private ProgrammingService programmingService;
        private bool taskActive;
        private bool TaskActive
        {
            get { return taskActive;}
            set
            {
                taskActive = value;
                if (taskActive)
                {
                    BeginInvoke((Action)(() =>
                    {
                        progressBarEvent.Style = ProgressBarStyle.Marquee;
                        labelProgressEvent.Text = string.Empty;
                    }));
                }
                else
                {
                    BeginInvoke((Action)(() =>
                    {
                        progressBarEvent.Style = ProgressBarStyle.Blocks;
                        progressBarEvent.Value = progressBarEvent.Minimum;
                        labelProgressEvent.Text = string.Empty;
                    }));
                }
            }
        }

        private PsdzContext psdzContext;

        public FormMain()
        {
            InitializeComponent();
        }

        private void UpdateDisplay()
        {
            bool active = TaskActive;
            bool hostRunning = false;
            bool vehicleConnected = false;
            bool talPresent = false;
            if (!active)
            {
                hostRunning = programmingService != null && programmingService.IsPsdzPsdzServiceHostInitialized();
            }

            if (hostRunning && psdzContext?.Connection != null)
            {
                vehicleConnected = true;
                talPresent = psdzContext?.Tal != null;
            }

            textBoxIstaFolder.Enabled = !active && !hostRunning;
            ipAddressControlVehicleIp.Enabled = !active && !vehicleConnected;
            buttonStartHost.Enabled = !active && !hostRunning;
            buttonStopHost.Enabled = !active && hostRunning;
            buttonConnect.Enabled = !active && hostRunning && !vehicleConnected;
            buttonDisconnect.Enabled = !active && hostRunning && vehicleConnected;
            buttonFunc1.Enabled = !active && hostRunning && vehicleConnected;
            buttonFunc2.Enabled = buttonFunc1.Enabled;
            buttonExecuteTal.Enabled = buttonFunc1.Enabled && talPresent;
            buttonClose.Enabled = !active;
            buttonAbort.Enabled = active;
        }

        private bool LoadSettings()
        {
            try
            {
                textBoxIstaFolder.Text = Properties.Settings.Default.IstaFolder;
                ipAddressControlVehicleIp.Text = Properties.Settings.Default.VehicleIp;
                if (string.IsNullOrWhiteSpace(ipAddressControlVehicleIp.Text.Trim('.')))
                {
                    ipAddressControlVehicleIp.Text = @"127.0.0.1";
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private bool StoreSettings()
        {
            try
            {
                Properties.Settings.Default.IstaFolder = textBoxIstaFolder.Text;
                Properties.Settings.Default.VehicleIp = ipAddressControlVehicleIp.Text;
                Properties.Settings.Default.Save();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private void UpdateStatus(string message = null)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() =>
                {
                    UpdateStatus(message);
                }));
                return;
            }

            textBoxStatus.Text = message ?? string.Empty;
            textBoxStatus.SelectionStart = textBoxStatus.TextLength;
            textBoxStatus.Update();
            textBoxStatus.ScrollToCaret();

            UpdateDisplay();
        }

        private async Task<bool> StartProgrammingServiceTask(string dealerId)
        {
            return await Task.Run(() => StartProgrammingService(dealerId)).ConfigureAwait(false);
        }

        private bool StartProgrammingService(string dealerId)
        {
            StringBuilder sbResult = new StringBuilder();
            try
            {
                sbResult.AppendLine("Starting host ...");
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "DealerId={0}", dealerId));
                UpdateStatus(sbResult.ToString());

                if (programmingService != null && programmingService.IsPsdzPsdzServiceHostInitialized())
                {
                    if (!StopProgrammingService())
                    {
                        sbResult.AppendLine("Stop host failed");
                        UpdateStatus(sbResult.ToString());
                        return false;
                    }
                }

                programmingService = new ProgrammingService(textBoxIstaFolder.Text, dealerId);
                programmingService.EventManager.ProgrammingEventRaised += (sender, args) =>
                {
                    if (args is ProgrammingTaskEventArgs programmingEventArgs)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            progressBarEvent.Style = ProgressBarStyle.Blocks;
                            if (programmingEventArgs.IsTaskFinished)
                            {
                                labelProgressEvent.Text = string.Empty;
                                progressBarEvent.Value = progressBarEvent.Maximum;
                            }
                            else
                            {
                                int progress = (int) (programmingEventArgs.Progress * 100.0);
                                labelProgressEvent.Text = string.Format(CultureInfo.InvariantCulture, "{0}%, {1}s", progress, programmingEventArgs.TimeLeftSec);
                                progressBarEvent.Value = progress;
                            }
                        }));
                    }
                    else if (args is ProgrammingActionStateChangedEventArgs programmingActionEventArgs)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            labelProgressEvent.Text = string.Format(CultureInfo.InvariantCulture, "Ecu={0}, State={1}, Type={2}",
                                programmingActionEventArgs.Ecu.ECU_NAME,
                                programmingActionEventArgs.ProgrammingActionState,
                                programmingActionEventArgs.ProgrammingActionType);
                        }));
                    }
                    else if (args is ProgrammingCurrentEcuChangedEventArgs programmingEcuEventArgs)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            int progress = (int)programmingEcuEventArgs.EcuProgrammingInfo.ProgressValue * 100;
                            progressBarEvent.Style = ProgressBarStyle.Blocks;
                            progressBarEvent.Value = progress;
                            labelProgressEvent.Text = string.Format(CultureInfo.InvariantCulture, "Ecu={0}, State={1}, {2}%",
                                programmingEcuEventArgs.EcuProgrammingInfo.Ecu.ECU_NAME,
                                programmingEcuEventArgs.EcuProgrammingInfo.State,
                                progress);
                        }));
                    }
                };

                programmingService.PsdzLoglevel = PsdzLoglevel.TRACE;
                programmingService.ProdiasLoglevel = ProdiasLoglevel.TRACE;
                if (!programmingService.StartPsdzServiceHost())
                {
                    sbResult.AppendLine("Start host failed");
                    UpdateStatus(sbResult.ToString());
                    return false;
                }

                sbResult.AppendLine("Host started");
                UpdateStatus(sbResult.ToString());
                return true;
            }
            catch (Exception ex)
            {
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", ex.Message));
                UpdateStatus(sbResult.ToString());
                return false;
            }
        }

        private async Task<bool> StopProgrammingServiceTask()
        {
            // ReSharper disable once ConvertClosureToMethodGroup
            return await Task.Run(() => StopProgrammingService()).ConfigureAwait(false);
        }

        private bool StopProgrammingService()
        {
            StringBuilder sbResult = new StringBuilder();
            try
            {
                sbResult.AppendLine("Stopping host ...");
                UpdateStatus(sbResult.ToString());

                if (programmingService != null)
                {
                    programmingService.Psdz.Shutdown();
                    programmingService.CloseConnectionsToPsdzHost();
                    programmingService.Dispose();
                    programmingService = null;
                    ClearProgrammingObjects();
                }

                sbResult.AppendLine("Host stopped");
                UpdateStatus(sbResult.ToString());
            }
            catch (Exception ex)
            {
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", ex.Message));
                UpdateStatus(sbResult.ToString());
                return false;
            }

            return true;
        }

        private async Task<bool> ConnectVehicleTask(string istaFolder, string url, string baureihe)
        {
            // ReSharper disable once ConvertClosureToMethodGroup
            return await Task.Run(() => ConnectVehicle(istaFolder, url, baureihe)).ConfigureAwait(false);
        }

        private bool ConnectVehicle(string istaFolder, string url, string baureihe)
        {
            StringBuilder sbResult = new StringBuilder();

            try
            {
                sbResult.AppendLine("Connecting vehicle ...");
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Url={0}, Series={1}", url, baureihe));
                UpdateStatus(sbResult.ToString());

                if (programmingService == null)
                {
                    return false;
                }

                if (!InitProgrammingObjects(istaFolder))
                {
                    return false;
                }

                string mainSeries = programmingService.Psdz.ConfigurationService.RequestBaureihenverbund(baureihe);
                IEnumerable<IPsdzTargetSelector> targetSelectors = programmingService.Psdz.ConnectionFactoryService.GetTargetSelectors();
                psdzContext.TargetSelectors = targetSelectors;
                TargetSelectorChooser targetSelectorChooser = new TargetSelectorChooser(psdzContext.TargetSelectors);
                IPsdzTargetSelector psdzTargetSelectorNewest = targetSelectorChooser.GetNewestTargetSelectorByMainSeries(mainSeries);
                if (psdzTargetSelectorNewest == null)
                {
                    sbResult.AppendLine("No target selector");
                    UpdateStatus(sbResult.ToString());
                    return false;
                }

                psdzContext.ProjectName = psdzTargetSelectorNewest.Project;
                psdzContext.VehicleInfo = psdzTargetSelectorNewest.VehicleInfo;
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Target selector: Project={0}, Vehicle={1}, Series={2}",
                    psdzTargetSelectorNewest.Project, psdzTargetSelectorNewest.VehicleInfo, psdzTargetSelectorNewest.Baureihenverbund));
                UpdateStatus(sbResult.ToString());

                string[] selectorParts = psdzTargetSelectorNewest.Project.Split('_');
                if (selectorParts.Length < 4)
                {
                    sbResult.AppendLine("Target selector not valid");
                    UpdateStatus(sbResult.ToString());
                    return false;
                }

                string dummyIStep = string.Join("-", selectorParts, 0, 4);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Dummy IStep: {0}", dummyIStep));

                IPsdzConnection psdzConnectionTemp = programmingService.Psdz.ConnectionManagerService.ConnectOverEthernet(psdzTargetSelectorNewest.Project, psdzTargetSelectorNewest.VehicleInfo, url, baureihe, dummyIStep);
                IPsdzIstufenTriple iStufenTriple = programmingService.Psdz.VcmService.GetIStufenTripleActual(psdzConnectionTemp);
                if (iStufenTriple == null)
                {
                    sbResult.AppendLine("Unable to read IStep triple");
                    UpdateStatus(sbResult.ToString());
                    return false;
                }

                programmingService.Psdz.ConnectionManagerService.CloseConnection(psdzConnectionTemp);
                string bauIStufe = iStufenTriple.Shipment;
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "IStep shipment: {0}", bauIStufe));

                IPsdzConnection psdzConnection = programmingService.Psdz.ConnectionManagerService.ConnectOverEthernet(psdzTargetSelectorNewest.Project, psdzTargetSelectorNewest.VehicleInfo, url, baureihe, bauIStufe);
                psdzContext.Connection = psdzConnection;

                sbResult.AppendLine("Vehicle connected");
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Connection: Id={0}, Port={1}", psdzConnection.Id, psdzConnection.Port));

                UpdateStatus(sbResult.ToString());

                programmingService.AddListener(psdzContext);
                return true;
            }
            catch (Exception ex)
            {
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", ex.Message));
                UpdateStatus(sbResult.ToString());
                return false;
            }
        }

        private async Task<bool> DisconnectVehicleTask()
        {
            // ReSharper disable once ConvertClosureToMethodGroup
            return await Task.Run(() => DisconnectVehicle()).ConfigureAwait(false);
        }

        private bool DisconnectVehicle()
        {
            StringBuilder sbResult = new StringBuilder();

            try
            {
                sbResult.AppendLine("Disconnecting vehicle ...");
                UpdateStatus(sbResult.ToString());

                if (programmingService == null)
                {
                    sbResult.AppendLine("No Host");
                    UpdateStatus(sbResult.ToString());
                    return false;
                }

                if (psdzContext?.Connection == null)
                {
                    sbResult.AppendLine("No connection");
                    UpdateStatus(sbResult.ToString());
                    return false;
                }

                programmingService.RemoveListener();
                programmingService.Psdz.ConnectionManagerService.CloseConnection(psdzContext.Connection);

                ClearProgrammingObjects();
                sbResult.AppendLine("Vehicle disconnected");
                UpdateStatus(sbResult.ToString());
                return true;
            }
            catch (Exception ex)
            {
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", ex.Message));
                UpdateStatus(sbResult.ToString());
                return false;
            }
        }

        private async Task<bool> VehicleFunctionsTask(bool executeTal, List<string> faRemList = null, List<string> faAddList = null)
        {
            return await Task.Run(() => VehicleFunctions(executeTal, faRemList, faAddList)).ConfigureAwait(false);
        }

        private bool VehicleFunctions(bool executeTal, List<string> faRemList, List<string> faAddList)
        {
            StringBuilder sbResult = new StringBuilder();

            try
            {
                sbResult.AppendLine("Executing vehicle functions ...");
                UpdateStatus(sbResult.ToString());

                if (programmingService == null)
                {
                    sbResult.AppendLine("No Host");
                    UpdateStatus(sbResult.ToString());
                    return false;
                }

                if (psdzContext?.Connection == null)
                {
                    sbResult.AppendLine("No connection");
                    UpdateStatus(sbResult.ToString());
                    return false;
                }

                IPsdzVin psdzVin = programmingService.Psdz.VcmService.GetVinFromMaster(psdzContext.Connection);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Vin: {0}", psdzVin.Value));
                UpdateStatus(sbResult.ToString());

                if (executeTal)
                {
                    sbResult.AppendLine("Execute TAL");
                    UpdateStatus(sbResult.ToString());

                    if (psdzContext.Tal == null)
                    {
                        sbResult.AppendLine("No TAL present");
                        UpdateStatus(sbResult.ToString());
                        return false;
                    }

                    DateTime calculationStartTime = DateTime.Now;
                    IEnumerable<IPsdzEcuIdentifier> psdzEcuIdentifiersPrg = programmingService.Psdz.ProgrammingService.CheckProgrammingCounter(psdzContext.Connection, psdzContext.Tal);
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "ProgCounter: {0}", psdzEcuIdentifiersPrg.Count()));
                    foreach (IPsdzEcuIdentifier ecuIdentifier in psdzEcuIdentifiersPrg)
                    {
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " EcuId: BaseVar={0}, DiagAddr={1}, DiagOffset={2}",
                            ecuIdentifier.BaseVariant, ecuIdentifier.DiagAddrAsInt, ecuIdentifier.DiagnosisAddress.Offset));
                    }
                    UpdateStatus(sbResult.ToString());

                    PsdzSecureCodingConfigCto secureCodingConfig = SecureCodingConfigWrapper.GetSecureCodingConfig(programmingService);
                    IPsdzCheckNcdResultEto psdzCheckNcdResultEto = programmingService.Psdz.SecureCodingService.CheckNcdAvailabilityForGivenTal(psdzContext.Tal, secureCodingConfig.NcdRootDirectory, psdzVin);
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Ncd EachSigned: {0}", psdzCheckNcdResultEto.isEachNcdSigned));
                    foreach (IPsdzDetailedNcdInfoEto detailedNcdInfo in psdzCheckNcdResultEto.DetailedNcdStatus)
                    {
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Ncd: Btld={0}, Cafd={1}, Status={2}",
                            detailedNcdInfo.Btld.HexString, detailedNcdInfo.Cafd.HexString, detailedNcdInfo.NcdStatus));
                    }
                    UpdateStatus(sbResult.ToString());

                    List<IPsdzRequestNcdEto> requestNcdEtos = ProgrammingUtils.CreateRequestNcdEtos(psdzCheckNcdResultEto);
#if false
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Ncd Requests: {0}", requestNcdEtos.Count));
                    foreach (IPsdzRequestNcdEto requestNcdEto in requestNcdEtos)
                    {
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Ncd for Cafd={0}, Btld={1}",
                            requestNcdEto.Cafd.Id, requestNcdEto.Btld.HexString));
                    }
                    UpdateStatus(sbResult.ToString());
#endif
                    string secureCodingPath = SecureCodingConfigWrapper.GetSecureCodingPathWithVin(programmingService, psdzVin.Value);
                    string jsonRequestFilePath = Path.Combine(secureCodingPath, string.Format(CultureInfo.InvariantCulture, "SecureCodingNCDCalculationRequest_{0}_{1}_{2}.json",
                        psdzVin.Value, DealerId, calculationStartTime.ToString("HHmmss", CultureInfo.InvariantCulture)));
                    PsdzBackendNcdCalculationEtoEnum backendNcdCalculationEtoEnumOld = secureCodingConfig.BackendNcdCalculationEtoEnum;
                    try
                    {
                        secureCodingConfig.BackendNcdCalculationEtoEnum = PsdzBackendNcdCalculationEtoEnum.ALLOW;
                        IList<IPsdzSecurityBackendRequestFailureCto> psdzSecurityBackendRequestFailureList = programmingService.Psdz.SecureCodingService.RequestCalculationNcdAndSignatureOffline(requestNcdEtos, jsonRequestFilePath, secureCodingConfig, psdzVin, psdzContext.FaTarget);
                        int failureCount = psdzSecurityBackendRequestFailureList.Count;
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Ncd failures: {0}", failureCount));
                        foreach (IPsdzSecurityBackendRequestFailureCto psdzSecurityBackendRequestFailure in psdzSecurityBackendRequestFailureList)
                        {
                            sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Failure: Cause={0}, Retry={1}, Url={2}",
                                psdzSecurityBackendRequestFailure.Cause, psdzSecurityBackendRequestFailure.Retry, psdzSecurityBackendRequestFailure.Url));
                        }
                        UpdateStatus(sbResult.ToString());

                        if (failureCount > 0)
                        {
                            sbResult.AppendLine("Ncd failures present");
                            UpdateStatus(sbResult.ToString());
                            return false;
                        }

                        if (!File.Exists(jsonRequestFilePath))
                        {
                            sbResult.AppendLine("Ncd request file not generated");
                            UpdateStatus(sbResult.ToString());
                            return false;
                        }

                        RequestJson requestJson = new JsonHelper().ReadRequestJson(jsonRequestFilePath);
                        if (requestJson == null || !ProgrammingUtils.CheckIfThereAreAnyNcdInTheRequest(requestJson))
                        {
                            sbResult.AppendLine("No ecu data in the request json file. Ncd calculation not required");
                        }

                        IEnumerable<string> cafdCalculatedInSCB = ProgrammingUtils.CafdCalculatedInSCB(requestJson);
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Cafd in SCB: {0}", cafdCalculatedInSCB.Count()));
                        foreach (string cafd in cafdCalculatedInSCB)
                        {
                            sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Cafd: {0}", cafd));
                        }
                        UpdateStatus(sbResult.ToString());

                        IEnumerable<IPsdzSgbmId> sweList = programmingService.Psdz.LogicService.RequestSweList(psdzContext.Tal, true);
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Swe list: {0}", sweList.Count()));
                        foreach (IPsdzSgbmId psdzSgbmId in sweList)
                        {
                            sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Sgbm: {0}", psdzSgbmId.HexString));
                        }
                        UpdateStatus(sbResult.ToString());

                        IEnumerable<IPsdzSgbmId> sgbmIds = ProgrammingUtils.RemoveCafdsCalculatedOnSCB(cafdCalculatedInSCB, sweList);
                        IEnumerable<IPsdzSgbmId> softwareEntries = programmingService.Psdz.MacrosService.CheckSoftwareEntries(sgbmIds);
                        int softwareEntryCount = softwareEntries.Count();
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Sw entries: {0}", softwareEntryCount));
                        foreach (IPsdzSgbmId psdzSgbmId in softwareEntries)
                        {
                            sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Sgbm: {0}", psdzSgbmId.HexString));
                        }
                        UpdateStatus(sbResult.ToString());

                        if (softwareEntryCount > 0)
                        {
                            sbResult.AppendLine("Software failures present");
                            UpdateStatus(sbResult.ToString());
                            return false;
                        }

                        TalExecutionSettings talExecutionSettings = ProgrammingUtils.GetTalExecutionSettings(programmingService);
                        IPsdzTal backupTalResult = programmingService.Psdz.IndividualDataRestoreService.ExecuteAsyncBackupTal(
                            psdzContext.Connection, psdzContext.IndividualDataBackupTal, null, psdzContext.FaTarget, psdzVin, talExecutionSettings, psdzContext.PathToBackupData);
                        sbResult.AppendLine("Backup Tal result:");
                        //sbResult.AppendLine(backupTalResult.AsXml);
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Size: {0}", backupTalResult.AsXml.Length));
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " State: {0}", backupTalResult.TalExecutionState));
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Ecus: {0}", backupTalResult.AffectedEcus.Count()));
                        UpdateStatus(sbResult.ToString());
                    }
                    finally
                    {
                        secureCodingConfig.BackendNcdCalculationEtoEnum = backendNcdCalculationEtoEnumOld;
                        if (Directory.Exists(secureCodingPath))
                        {
                            try
                            {
                                Directory.Delete(secureCodingPath, true);
                            }
                            catch (Exception ex)
                            {
                                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Directory exception: {0}", ex.Message));
                                UpdateStatus(sbResult.ToString());
                            }
                        }
                    }

                    return true;
                }

                bool bModifyFa = false;
                if (faRemList != null && faAddList != null && (faRemList.Count > 0 || faAddList.Count > 0))
                {
                    bModifyFa = true;
                    sbResult.Append("FaRem:");
                    foreach (string faRem in faRemList)
                    {
                        sbResult.Append(" ");
                        sbResult.Append(faRem);
                    }

                    sbResult.AppendLine();

                    sbResult.Append("FaAdd:");
                    foreach (string faAdd in faAddList)
                    {
                        sbResult.Append(" ");
                        sbResult.Append(faAdd);
                    }

                    sbResult.AppendLine();
                }

                IPsdzTalFilter psdzTalFilter = programmingService.Psdz.ObjectBuilder.BuildTalFilter();
                // disable backup
                psdzTalFilter = programmingService.Psdz.ObjectBuilder.DefineFilterForAllEcus(new[] { TaCategories.FscBackup }, TalFilterOptions.MustNot, psdzTalFilter);
                if (bModifyFa)
                {   // enable deploy
                    psdzTalFilter = programmingService.Psdz.ObjectBuilder.DefineFilterForAllEcus(new[] { TaCategories.CdDeploy }, TalFilterOptions.Must, psdzTalFilter);
                }
                psdzContext.SetTalFilter(psdzTalFilter);

                IPsdzTalFilter psdzTalFilterEmpty = programmingService.Psdz.ObjectBuilder.BuildTalFilter();
                psdzContext.SetTalFilterForIndividualDataTal(psdzTalFilterEmpty);

                if (psdzContext.TalFilter != null)
                {
                    sbResult.AppendLine("TalFilter:");
                    sbResult.Append(psdzContext.TalFilter.AsXml);
                }

                psdzContext.CleanupBackupData();
                IPsdzIstufenTriple iStufenTriple = programmingService.Psdz.VcmService.GetIStufenTripleActual(psdzContext.Connection);
                psdzContext.SetIstufen(iStufenTriple);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "IStep: Current={0}, Last={1}, Shipment={2}",
                    iStufenTriple.Current, iStufenTriple.Last, iStufenTriple.Shipment));

                if (!psdzContext.SetPathToBackupData(psdzVin.Value))
                {
                    sbResult.AppendLine("Create backup path failed");
                    return false;
                }

                IPsdzStandardFa standardFa = programmingService.Psdz.VcmService.GetStandardFaActual(psdzContext.Connection);
                IPsdzFa psdzFa = programmingService.Psdz.ObjectBuilder.BuildFa(standardFa, psdzVin.Value);
                psdzContext.SetFaActual(psdzFa);
                sbResult.AppendLine("FA current:");
                sbResult.Append(psdzFa.AsXml);
                UpdateStatus(sbResult.ToString());

                if (bModifyFa)
                {
                    IFa ifaTarget = ProgrammingUtils.BuildFa(standardFa);
                    if (!ProgrammingUtils.ModifyFa(ifaTarget, faRemList, false))
                    {
                        sbResult.AppendLine("ModifyFa remove failed");
                        return false;
                    }

                    if (!ProgrammingUtils.ModifyFa(ifaTarget, faAddList, true))
                    {
                        sbResult.AppendLine("ModifyFa add failed");
                        return false;
                    }

                    IPsdzFa psdzFaTarget = programmingService.Psdz.ObjectBuilder.BuildFa(ifaTarget, psdzVin.Value);
                    psdzContext.SetFaTarget(psdzFaTarget);

                    sbResult.AppendLine("FA target:");
                    sbResult.Append(psdzFaTarget.AsXml);
                }
                else
                {
                    psdzContext.SetFaTarget(psdzFa);
                }

                IEnumerable<IPsdzIstufe> psdzIstufes = programmingService.Psdz.LogicService.GetPossibleIntegrationLevel(psdzContext.FaTarget);
                psdzContext.SetPossibleIstufenTarget(psdzIstufes);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "ISteps: {0}", psdzIstufes.Count()));
                foreach (IPsdzIstufe iStufe in psdzIstufes.OrderBy(x => x))
                {
                    if (iStufe.IsValid)
                    {
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " IStep: {0}", iStufe.Value));
                    }
                }
                UpdateStatus(sbResult.ToString());

                string latestIstufeTarget = psdzContext.LatestPossibleIstufeTarget;
                if (string.IsNullOrEmpty(latestIstufeTarget))
                {
                    sbResult.AppendLine("No target iStep");
                    return false;
                }
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "IStep Latest: {0}", latestIstufeTarget));

                IPsdzIstufe psdzIstufeShip = programmingService.Psdz.ObjectBuilder.BuildIstufe(psdzContext.IstufeShipment);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "IStep Ship: {0}", psdzIstufeShip.Value));

                IPsdzIstufe psdzIstufeTarget = programmingService.Psdz.ObjectBuilder.BuildIstufe(bModifyFa ? psdzContext.IstufeCurrent : latestIstufeTarget);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "IStep Target: {0}", psdzIstufeTarget.Value));
                UpdateStatus(sbResult.ToString());

                IEnumerable<IPsdzEcuIdentifier> psdzEcuIdentifiers = programmingService.Psdz.MacrosService.GetInstalledEcuList(psdzContext.FaActual, psdzIstufeShip);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "EcuIds: {0}", psdzEcuIdentifiers.Count()));
                foreach (IPsdzEcuIdentifier ecuIdentifier in psdzEcuIdentifiers)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " EcuId: BaseVar={0}, DiagAddr={1}, DiagOffset={2}",
                        ecuIdentifier.BaseVariant, ecuIdentifier.DiagAddrAsInt, ecuIdentifier.DiagnosisAddress.Offset));
                }
                UpdateStatus(sbResult.ToString());

                IPsdzStandardSvt psdzStandardSvt = programmingService.Psdz.EcuService.RequestSvt(psdzContext.Connection, psdzEcuIdentifiers);
                IPsdzStandardSvt psdzStandardSvtNames = programmingService.Psdz.LogicService.FillBntnNamesForMainSeries(psdzContext.Connection.TargetSelector.Baureihenverbund, psdzStandardSvt);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Svt Ecus: {0}", psdzStandardSvtNames.Ecus.Count()));
                foreach (IPsdzEcu ecu in psdzStandardSvtNames.Ecus)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Variant: BaseVar={0}, Var={1}, Name={2}",
                        ecu.BaseVariant, ecu.EcuVariant, ecu.BnTnName));
                }

                IPsdzSvt psdzSvt = programmingService.Psdz.ObjectBuilder.BuildSvt(psdzStandardSvtNames, psdzVin.Value);
                psdzContext.SetSvtActual(psdzSvt);
                UpdateStatus(sbResult.ToString());

                IPsdzReadEcuUidResultCto psdzReadEcuUid = programmingService.Psdz.SecurityManagementService.readEcuUid(psdzContext.Connection, psdzEcuIdentifiers, psdzContext.SvtActual);

                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "EcuUids: {0}", psdzReadEcuUid.EcuUids.Count));
                foreach (KeyValuePair<IPsdzEcuIdentifier, IPsdzEcuUidCto> ecuUid in psdzReadEcuUid.EcuUids)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " EcuId: BaseVar={0}, DiagAddr={1}, DiagOffset={2}, Uid={3}",
                        ecuUid.Key.BaseVariant, ecuUid.Key.DiagAddrAsInt, ecuUid.Key.DiagnosisAddress.Offset, ecuUid.Value.EcuUid));
                }
#if false
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "EcuUid failures: {0}", psdzReadEcuUid.FailureResponse.Count()));
                foreach (IPsdzEcuFailureResponseCto failureResponse in psdzReadEcuUid.FailureResponse)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Fail: BaseVar={0}, DiagAddr={1}, DiagOffset={2}, Cause={3}",
                        failureResponse.EcuIdentifierCto.BaseVariant, failureResponse.EcuIdentifierCto.DiagAddrAsInt, failureResponse.EcuIdentifierCto.DiagnosisAddress.Offset,
                        failureResponse.Cause.Description));
                }
#endif
                UpdateStatus(sbResult.ToString());

                IPsdzReadStatusResultCto psdzReadStatusResult = programmingService.Psdz.SecureFeatureActivationService.ReadStatus(PsdzStatusRequestFeatureTypeEtoEnum.ALL_FEATURES, psdzContext.Connection, psdzContext.SvtActual, psdzEcuIdentifiers, true, 3, 100);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Status failures: {0}", psdzReadStatusResult.Failures.Count()));
#if false
                foreach (IPsdzEcuFailureResponseCto failureResponse in psdzReadStatusResult.Failures)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Fail: BaseVar={0}, DiagAddr={1}, DiagOffset={2}, Cause={3}",
                        failureResponse.EcuIdentifierCto.BaseVariant, failureResponse.EcuIdentifierCto.DiagAddrAsInt, failureResponse.EcuIdentifierCto.DiagnosisAddress.Offset,
                        failureResponse.Cause.Description));
                }
#endif
                UpdateStatus(sbResult.ToString());

                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Status features: {0}", psdzReadStatusResult.FeatureStatusSet.Count()));
                foreach (IPsdzFeatureLongStatusCto featureLongStatus in psdzReadStatusResult.FeatureStatusSet)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Feature: BaseVar={0}, DiagAddr={1}, DiagOffset={2}, Status={3}, Token={4}",
                        featureLongStatus.EcuIdentifierCto.BaseVariant, featureLongStatus.EcuIdentifierCto.DiagAddrAsInt, featureLongStatus.EcuIdentifierCto.DiagnosisAddress.Offset,
                        featureLongStatus.FeatureStatusEto, featureLongStatus.TokenId));
                }
                UpdateStatus(sbResult.ToString());

                IPsdzTalFilter talFilterFlash = new PsdzTalFilter();
                IPsdzSollverbauung psdzSollverbauung = programmingService.Psdz.LogicService.GenerateSollverbauungGesamtFlash(psdzContext.Connection, psdzIstufeTarget, psdzIstufeShip, psdzContext.SvtActual, psdzContext.FaTarget, talFilterFlash);
                psdzContext.SetSollverbauung(psdzSollverbauung);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Target construction: Count={0}, Units={1}",
                    psdzSollverbauung.PsdzOrderList.BntnVariantInstances.Length, psdzSollverbauung.PsdzOrderList.NumberOfUnits));
                foreach (IPsdzEcuVariantInstance bntnVariant in psdzSollverbauung.PsdzOrderList.BntnVariantInstances)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Variant: BaseVar={0}, Var={1}, Name={2}",
                        bntnVariant.Ecu.BaseVariant, bntnVariant.Ecu.EcuVariant, bntnVariant.Ecu.BnTnName));
                }
                UpdateStatus(sbResult.ToString());

                IEnumerable<IPsdzEcuContextInfo> psdzEcuContextInfos = programmingService.Psdz.EcuService.RequestEcuContextInfos(psdzContext.Connection, psdzEcuIdentifiers);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Ecu contexts: {0}", psdzEcuContextInfos.Count()));
                foreach (IPsdzEcuContextInfo ecuContextInfo in psdzEcuContextInfos)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Ecu context: BaseVar={0}, DiagAddr={1}, DiagOffset={2}, ManuDate={3}, PrgDate={4}, PrgCnt={5}, FlashCnt={6}, FlashRemain={7}",
                        ecuContextInfo.EcuId.BaseVariant, ecuContextInfo.EcuId.DiagAddrAsInt, ecuContextInfo.EcuId.DiagnosisAddress.Offset,
                        ecuContextInfo.ManufacturingDate, ecuContextInfo.LastProgrammingDate, ecuContextInfo.ProgramCounter, ecuContextInfo.PerformedFlashCycles, ecuContextInfo.RemainingFlashCycles));
                }
                UpdateStatus(sbResult.ToString());

                IPsdzSwtAction psdzSwtAction = programmingService.Psdz.ProgrammingService.RequestSwtAction(psdzContext.Connection, true);
                psdzContext.SwtAction = psdzSwtAction;
                if (psdzSwtAction?.SwtEcus != null)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Swt Ecus: {0}", psdzSwtAction.SwtEcus.Count()));
                    foreach (IPsdzSwtEcu psdzSwtEcu in psdzSwtAction.SwtEcus)
                    {
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Ecu: Id={0}, Vin={1}, CertState={2}, SwSig={3}",
                            psdzSwtEcu.EcuIdentifier, psdzSwtEcu.Vin, psdzSwtEcu.RootCertState, psdzSwtEcu.SoftwareSigState));
                        foreach (IPsdzSwtApplication swtApplication in psdzSwtEcu.SwtApplications)
                        {
                            sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Fsc: Type={0}, State={1}, Length={2}",
                                swtApplication.SwtType, swtApplication.FscState,  swtApplication.Fsc.Length));
                        }
                    }
                }
                UpdateStatus(sbResult.ToString());

                IPsdzTal psdzTal = programmingService.Psdz.LogicService.GenerateTal(psdzContext.Connection, psdzContext.SvtActual, psdzSollverbauung, psdzContext.SwtAction, psdzContext.TalFilter, psdzContext.FaActual.Vin);
                psdzContext.Tal = psdzTal;
                sbResult.AppendLine("Tal:");
                //sbResult.AppendLine(psdzTal.AsXml);
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Size: {0}", psdzTal.AsXml.Length));
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " State: {0}", psdzTal.TalExecutionState));
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Ecus: {0}", psdzTal.AffectedEcus.Count()));
                foreach (IPsdzEcuIdentifier ecuIdentifier in psdzTal.AffectedEcus)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "  Affected Ecu: BaseVar={0}, DiagAddr={1}, DiagOffset={2}",
                        ecuIdentifier.BaseVariant, ecuIdentifier.DiagAddrAsInt, ecuIdentifier.DiagnosisAddress.Offset));
                }

                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Lines: {0}", psdzTal.TalLines.Count()));
                foreach (IPsdzTalLine talLine in psdzTal.TalLines)
                {
                    sbResult.Append(string.Format(CultureInfo.InvariantCulture, "  Tal line: BaseVar={0}, DiagAddr={1}, DiagOffset={2}",
                        talLine.EcuIdentifier.BaseVariant, talLine.EcuIdentifier.DiagAddrAsInt, talLine.EcuIdentifier.DiagnosisAddress.Offset));
                    sbResult.Append(string.Format(CultureInfo.InvariantCulture, " FscDeploy={0}, BlFlash={1}, IbaDeploy={2}, SwDeploy={3}, IdRestore={4}, SfaDeploy={5}, Cat={6}",
                        talLine.FscDeploy.Tas.Count(), talLine.BlFlash.Tas.Count(), talLine.IbaDeploy.Tas.Count(),
                        talLine.SwDeploy.Tas.Count(), talLine.IdRestore.Tas.Count(), talLine.SFADeploy.Tas.Count(),
                        talLine.TaCategories));
                    sbResult.AppendLine();
                    foreach (IPsdzTa psdzTa in talLine.TaCategory.Tas)
                    {
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "   SgbmId={0}, State={1}",
                            psdzTa.SgbmId.HexString, psdzTa.ExecutionState));
                    }
                }

                if (psdzTal.HasFailureCauses)
                {
                    sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Failures: {0}", psdzTal.FailureCauses.Count()));
                    foreach (IPsdzFailureCause failureCause in psdzTal.FailureCauses)
                    {
                        sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "  Failure cause: {0}", failureCause.Message));
                    }
                }
                UpdateStatus(sbResult.ToString());

                IPsdzTal psdzBackupTal = programmingService.Psdz.IndividualDataRestoreService.GenerateBackupTal(psdzContext.Connection, psdzContext.PathToBackupData, psdzContext.Tal, psdzContext.TalFilter);
                psdzContext.IndividualDataBackupTal = psdzBackupTal;
                sbResult.AppendLine("Backup Tal:");
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Size: {0}", psdzBackupTal.AsXml.Length));
                UpdateStatus(sbResult.ToString());

                IPsdzTal psdzRestorePrognosisTal = programmingService.Psdz.IndividualDataRestoreService.GenerateRestorePrognosisTal(psdzContext.Connection, psdzContext.PathToBackupData, psdzContext.Tal, psdzContext.IndividualDataBackupTal, psdzContext.TalFilterForIndividualDataTal);
                psdzContext.IndividualDataRestorePrognosisTal = psdzRestorePrognosisTal;
                sbResult.AppendLine("Restore prognosis Tal:");
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, " Size: {0}", psdzRestorePrognosisTal.AsXml.Length));

                UpdateStatus(sbResult.ToString());
                return true;
            }
            catch (Exception ex)
            {
                sbResult.AppendLine(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", ex.Message));
                UpdateStatus(sbResult.ToString());
                return false;
            }
        }

        private bool InitProgrammingObjects(string istaFolder)
        {
            try
            {
                psdzContext = new PsdzContext(istaFolder);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private void ClearProgrammingObjects()
        {
            if (psdzContext != null)
            {
                psdzContext.CleanupBackupData();
                psdzContext = null;
            }
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void buttonAbort_Click(object sender, EventArgs e)
        {

        }

        private void buttonIstaFolder_Click(object sender, EventArgs e)
        {
            folderBrowserDialogIsta.SelectedPath = textBoxIstaFolder.Text;
            DialogResult result = folderBrowserDialogIsta.ShowDialog();
            if (result == DialogResult.OK)
            {
                textBoxIstaFolder.Text = folderBrowserDialogIsta.SelectedPath;
                UpdateDisplay();
            }
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            UpdateDisplay();
            StoreSettings();
            timerUpdate.Enabled = false;
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            LoadSettings();
            UpdateDisplay();
            UpdateStatus();
            timerUpdate.Enabled = true;
            labelProgressEvent.Text = string.Empty;
        }

        private void timerUpdate_Tick(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void buttonStartHost_Click(object sender, EventArgs e)
        {
            StartProgrammingServiceTask(DealerId).ContinueWith(task =>
            {
                TaskActive = false;
            });

            TaskActive = true;
            UpdateDisplay();
        }

        private void buttonStopHost_Click(object sender, EventArgs e)
        {
            StopProgrammingServiceTask().ContinueWith(task =>
            {
                TaskActive = false;
                if (e == null)
                {
                    BeginInvoke((Action)(() =>
                    {
                        Close();
                    }));
                }
            });

            TaskActive = true;
            UpdateDisplay();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (TaskActive)
            {
                e.Cancel = true;
                return;
            }

            if (programmingService != null && programmingService.IsPsdzPsdzServiceHostInitialized())
            {
                buttonStopHost_Click(sender, null);
                e.Cancel = true;
            }
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (psdzContext?.Connection != null)
            {
                return;
            }

            string url = "tcp://" + ipAddressControlVehicleIp.Text + ":6801";
            ConnectVehicleTask(textBoxIstaFolder.Text, url, Baureihe).ContinueWith(task =>
            {
                TaskActive = false;
            });

            TaskActive = true;
            UpdateDisplay();
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            if (psdzContext?.Connection == null)
            {
                return;
            }

            DisconnectVehicleTask().ContinueWith(task =>
            {
                TaskActive = false;
            });

            TaskActive = true;
            UpdateDisplay();
        }

        private void buttonFunc_Click(object sender, EventArgs e)
        {
            if (psdzContext?.Connection == null)
            {
                return;
            }

            bool executeTal = false;
            List<string> faRemList = null;
            List<string> faAddList = null;
            if (sender == buttonFunc2)
            {
                faRemList = new List<string>();
                faAddList = new List<string>();

                faRemList.AddRange(new [] {"$8KA", "$8KC", "$8KD", "$8KE", "$8KF", "$8KG", "$8KH", "$8KK", "$8KL", "$8KM", "$8KN", "$8KP", "$984", "$988", "$8ST" });
                faAddList.AddRange(new[] { "+MFSG", "+ATEF", "$8KB" });
            }
            else if (sender == buttonExecuteTal)
            {
                executeTal = true;
            }

            VehicleFunctionsTask(executeTal, faRemList, faAddList).ContinueWith(task =>
            {
                TaskActive = false;
            });

            TaskActive = true;
            UpdateDisplay();
        }
    }
}
