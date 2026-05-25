global using static MakerPrompt.UI.Components.Utils.Enums;
global using System.Text;
global using System.Text.Json;
global using System.Text.RegularExpressions;
global using System.Text.Json.Serialization;
global using System.Reflection;
global using System.Numerics;
global using System.Net;
global using System.Net.Http.Headers;
global using MakerPrompt.UI.Components.Infrastructure;
global using MakerPrompt.UI.Components.Properties;
global using MakerPrompt.UI.Components.Services;
global using MakerPrompt.UI.Components.Models;
global using MakerPrompt.UI.Components.Utils;
global using Microsoft.JSInterop;

// ── Model consolidation — Core types replace UI.Components duplicates ───────────
global using PrinterConnectionType = MakerPrompt.Core.Models.PrinterConnectionType;
global using PrinterConnectionSettings = MakerPrompt.Core.Models.PrinterConnectionSettings;
global using FarmConfiguration = MakerPrompt.Core.Models.FarmConfiguration;
global using PrinterConnectionDefinition = MakerPrompt.Core.Models.PrinterConnectionDefinition;
global using FilamentSpool = MakerPrompt.Core.Models.FilamentSpool;
global using NotificationLevel = MakerPrompt.Core.Models.NotificationLevel;
global using NotificationRecord = MakerPrompt.Core.Models.NotificationRecord;
global using PrintJobUsageRecord = MakerPrompt.Core.Models.PrintJobUsageRecord;
global using PrintProject = MakerPrompt.Core.Models.PrintProject;
global using PrintJob = MakerPrompt.Core.Models.PrintJob;
global using PrintJobStatus = MakerPrompt.Core.Models.PrintJobStatus;