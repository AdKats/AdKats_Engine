/*
 * AdKats Administration Engine for Procon Frostbite.
 * 
 * Copyright 2013-2015 A Different Kind, LLC
 * 
 * AdKats was inspired by the gaming community A Different Kind (ADK). Visit http://www.ADKGamers.com/ for more information.
 *
 * The AdKats engine is free software: You can redistribute it and/or modify it under the terms of the
 * GNU General Public License as published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version. AdKats is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See the GNU General Public License for more details. To view this license, visit http://www.gnu.org/licenses/.
 * 
 * Development by Daniel J. Gradinjan (ColColonCleaner)
 * 
 * AdKatsEngine.cs
 * Version 0.0.0.1
 * 
 * UNKNOWN RELEASE DATE
 * 
 * Automatic Update Information
 * <version_code>0.0.0.1</version_code>
 */

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using PRoCon.Core;
using PRoCon.Core.Maps;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

#endregion

namespace PRoConEvents {

    public class AdKatsEngine : PRoConPluginAPI, IPRoConPluginInterface {

        public enum GameVersion {
            BF3,
            BF4,
            BFHL
        }

        //Current engine version
        private const String EngineVersion = "0.0.0.1";

        //Interop
        public static List<AdKatsEngine> Instances;

        public readonly ExecutionMananger Exe;
        public readonly EventManager Events;
        public readonly InstanceManager Instance;
        public readonly LogManager Log;

        static AdKatsEngine() {
            Instances = new List<AdKatsEngine>();
        }

        public AdKatsEngine() {
            //Load interop
            Instances.Add(this);
            //Load managers
            Log = new LogManager(this) {
                CurrentDebugLevel = LogManager.DebugLevel.Basic
            };
            Exe = new ExecutionMananger(this);
            Events = new EventManager(this);
            Instance = new InstanceManager(this);
        }

        public string GetPluginName() {
            return "AdKats - Administration Engine";
        }

        public string GetPluginVersion() {
            return EngineVersion;
        }

        public string GetPluginAuthor() {
            return "[ADK]ColColonCleaner";
        }

        public string GetPluginWebsite() {
            return "https://github.com/AdKats/";
        }

        public void OnPluginEnable() {
            Log.ProconEventEnter();
            try {
                //Handle procon enable change
                Instance.OnProconEnabled(true);
            }
            catch (Exception e) {
                Log.Exception("Error handling event " + Helper.GetCurrentMethod(), e);
            }
            Log.ProconEventExit();
        }

        public void OnPluginDisable() {
            Log.ProconEventEnter();
            try {
                //Handle procon enable change
                Instance.OnProconEnabled(false);
            }
            catch (Exception e) {
                Log.Exception("Error handling event " + Helper.GetCurrentMethod(), e);
            }
            Log.ProconEventExit();
        }

        public class EventManager {
            //Plugin reference
            private readonly AdKatsEngine _engine;

            public EventManager(AdKatsEngine engine) {
                _engine = engine;
            }

            public class Event {
                public String Key { get; private set; }
                public String Name { get; private set; }
                public HashSet<GameVersion> Games { get; private set; }
                public Func<Object, Object, Object, Object, Boolean> TriggerFunction { get; private set; }
                public Func<Object, Object, Object, Object, Boolean> DefaultHandler { get; private set; }
                public Dictionary<String, Func<Object, Object, Object, Object, Boolean>> HandlerMethods { get; private set; }

                public Event(String key, String name, HashSet<GameVersion> games, Func<Object, Object, Object, Object, Boolean> defaultHandler) {
                    AdKatsEngine engine = Instances.First();
                    Key = key;
                    Name = name;
                    Games = games;
                    HandlerMethods = new Dictionary<String, Func<Object, Object, Object, Object, Boolean>>();
                    DefaultHandler = defaultHandler;
                    TriggerFunction = (o1, o2, o3, o4) => {
                        engine.Log.Debug("Triggering event " + key, LogManager.DebugLevel.Trace);
                        bool runDefault = true;
                        Stopwatch timer = new Stopwatch();
                        if (HandlerMethods.Any()) {
                            engine.Log.Debug(key + " has custom event handlers.", LogManager.DebugLevel.Trace);
                            lock (HandlerMethods) {
                                foreach (KeyValuePair<string, Func<object, object, object, object, bool>> handlerPair in HandlerMethods) {
                                    string subscriber = handlerPair.Key;
                                    Func<object, object, object, object, bool> handler = handlerPair.Value;
                                    engine.Log.Debug("Running " + subscriber + " event handler for " + key, LogManager.DebugLevel.Trace);
                                    bool response = true;
                                    timer.Reset();
                                    timer.Start();
                                    bool exception = false;
                                    try {
                                        response = handler(o1, o2, o3, o4);
                                    }
                                    catch (Exception e) {
                                        exception = true;
                                        engine.Log.Exception("Error running " + subscriber + " event handler for " + key, e);
                                    }
                                    timer.Stop();
                                    if (!exception) {
                                        if (response) {
                                            engine.Log.Debug(subscriber + " event handler for " + key + " returned in " + Helper.GetTimeS(timer.Elapsed) + ".", LogManager.DebugLevel.Trace);
                                        }
                                        else {
                                            runDefault = false;
                                            engine.Log.Debug(subscriber + " event handler for " + key + " returned in " + Helper.GetTimeS(timer.Elapsed) + ". Default handling cancelled.", LogManager.DebugLevel.Trace);
                                        }
                                    }
                                }
                            }
                        }
                        if (runDefault) {
                            timer.Reset();
                            timer.Start();
                            bool exception = false;
                            try {
                                DefaultHandler(o1, o2, o3, o4);
                            }
                            catch (Exception e) {
                                exception = true;
                                engine.Log.Exception("Error running default event handler for " + key, e);
                            }
                            timer.Stop();
                            if (!exception) {
                                engine.Log.Debug("Default event handler for " + key + " returned in " + Helper.GetTimeS(timer.Elapsed) + ".", LogManager.DebugLevel.Trace);
                            }
                        }
                        return runDefault;
                    };
                }

                public Boolean RegisterHandler(String source, Func<Object, Object, Object, Object, Boolean> handler) {
                    if (!HandlerMethods.ContainsKey(source)) {
                        lock (HandlerMethods) {
                            HandlerMethods[source] = handler;
                        }
                        return true;
                    }
                    return false;
                }

                public Boolean UnregisterHandler(String source) {
                    lock (HandlerMethods) {
                        return HandlerMethods.Remove(source);
                    }
                }
            }
        }

        public class ExecutionMananger {
            private readonly AdKatsEngine _engine;
            private readonly Queue<FunctionPackage> _functionRegistrationQueue = new Queue<FunctionPackage>();
            private readonly EventWaitHandle _functionRegistrationWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            private Thread _functionRegistrationThread;
            private readonly Dictionary<Int32, ThreadWatchDog> _registeredThreads = new Dictionary<Int32, ThreadWatchDog>();
            private Int32 _ranFunctions;

            public ExecutionMananger(AdKatsEngine engine) {
                try {
                    //References
                    _engine = engine;
                    //Start threads
                    StartThreadRegistration();
                }
                catch (Exception e) {
                    _engine.Log.Exception("Error while starting Thread Manager. " + _engine.GetType().Name + " in exception state.", e);
                }
            }

            public Int32 GetThreadCount() {
                return _registeredThreads.Count;
            }

            public void KickWatchdog() {
                ThreadWatchDog watchDog;
                if (_registeredThreads.TryGetValue(Thread.CurrentThread.ManagedThreadId, out watchDog)) {
                    watchDog.Kick();
                }
                else {
                    _engine.Log.Warn("Attempted to kick watchdog for unmonitored thread.");
                }
            }

            public void Async(String name, Func<Boolean> function) {
                Async(name, function, true);
            }

            public void Async(String name, Func<Boolean> function, Boolean register) {
                try {
                    lock (_functionRegistrationQueue) {
                        _engine.Log.Debug("Queueing function '" + name + "' to run async.", LogManager.DebugLevel.Trace);
                        _functionRegistrationQueue.Enqueue(new FunctionPackage() {
                            Name = name,
                            Function = function,
                            Register = register
                        });
                        _functionRegistrationWaitHandle.Set();
                    }
                }
                catch (Exception e) {
                    _engine.Log.Exception("Error handling " + Helper.GetCurrentMethod(), e);
                }
            }

            public Boolean RunSafe(String name, Func<Boolean> function) {
                try {
                    //Return whether to continue the calling stack
                    return function();
                }
                catch (Exception e) {
                    _engine.Log.Exception("Error while executing '" + name + "' function code.", e);
                }
                //Always continue calling stack in case of exception
                return true;
            }

            private void StartThreadRegistration() {
                //Thread registration
                _functionRegistrationWaitHandle.Reset();
                _functionRegistrationThread = new Thread(new ThreadStart(delegate {
                    try {
                        //Run permanently
                        Queue<FunctionPackage> inboundFunctions = new Queue<FunctionPackage>();
                        while (true) {
                            try {
                                //check, lock, import, unlock
                                if (_functionRegistrationQueue.Any()) {
                                    lock (_functionRegistrationQueue) {
                                        while (_functionRegistrationQueue.Any()) {
                                            inboundFunctions.Enqueue(_functionRegistrationQueue.Dequeue());
                                        }
                                    }
                                }
                                while (inboundFunctions.Any()) {
                                    FunctionPackage inboundFunction = inboundFunctions.Dequeue();
                                    _engine.Log.Debug("Async function '" + inboundFunction.Name + "' starting.", LogManager.DebugLevel.Trace);
                                    try {
                                        //Build thread name
                                        String threadName = Helper.AlphaNum(inboundFunction.Name);
                                        //Create thread
                                        EventWaitHandle threadRegisterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
                                        threadRegisterWaitHandle.Reset();
                                        Thread aThread = new Thread(new ThreadStart(delegate {
                                            //Run the given function safely
                                            bool result = RunSafe(inboundFunction.Name, inboundFunction.Function);
                                            _engine.Log.Debug("Async function '" + inboundFunction.Name + "' returned " + result.ToString().ToUpper() + ".", LogManager.DebugLevel.Trace);
                                            if (inboundFunction.Register) {
                                                //Wait to ensure registered thread
                                                threadRegisterWaitHandle.WaitOne(Timeout.Infinite);
                                                //Unregister thread
                                                lock (_registeredThreads) {
                                                    _registeredThreads.Remove(Thread.CurrentThread.ManagedThreadId);
                                                    _engine.Log.Debug("Async function '" + inboundFunction.Name + "' thread " + Thread.CurrentThread.ManagedThreadId + " ending.", LogManager.DebugLevel.Trace);
                                                }
                                            }
                                            Interlocked.Increment(ref _ranFunctions);
                                        })) {
                                            Name = threadName,
                                            IsBackground = true
                                        };
                                        //Run thread
                                        aThread.Start();
                                        if (inboundFunction.Register) {
                                            //Register thread
                                            lock (_registeredThreads) {
                                                if (!_registeredThreads.ContainsKey(aThread.ManagedThreadId)) {
                                                    //Add new watchdog
                                                    _registeredThreads.Add(aThread.ManagedThreadId, new ThreadWatchDog(aThread));
                                                }
                                                _engine.Log.Debug("Async function '" + inboundFunction.Name + "' assigned to thread " + aThread.ManagedThreadId + ".", LogManager.DebugLevel.Trace);
                                            }
                                            threadRegisterWaitHandle.Set();
                                        }
                                    }
                                    catch (Exception e) {
                                        _engine.Log.Exception("Error attempting to async run function '" + inboundFunction.Name + "'.", e);
                                    }
                                }
                                if (!_functionRegistrationQueue.Any()) {
                                    _functionRegistrationWaitHandle.Reset();
                                    _functionRegistrationWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                                }
                            }
                            catch (Exception e) {
                                _engine.Log.Exception("Error in thread registration loop. Skipping.", e);
                                Thread.Sleep(TimeSpan.FromSeconds(10));
                            }
                        }
                    }
                    catch (Exception e) {
                        _engine.Log.Exception("Exception running thread registration. " + _engine.GetType().Name + " in exception state.", e);
                    }
                })) {
                    Name = "ThreadRegistration",
                    IsBackground = true
                };
                _functionRegistrationThread.Start();
            }

            public class FunctionPackage {
                public Func<Boolean> Function;
                public String Name;
                public Boolean Register;
            }

            public class ThreadWatchDog {
                public Thread Thread;

                public ThreadWatchDog(Thread thread) {
                    Thread = thread;
                    Timestamp = DateTime.UtcNow;
                }

                public DateTime Timestamp { get; private set; }

                public void Kick() {
                    Timestamp = DateTime.UtcNow;
                }
            }
        }

        public class InstanceManager {
            public enum InstanceState {
                Setup,
                Stopped,
                Starting,
                Running,
                Stopping,
                Exception
            }

            private readonly EventWaitHandle _instanceMonitorWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            private readonly AdKatsEngine _engine;

            //Procon enabled is the goal for current state
            public Boolean ProconEnabled { get; private set; }
            public InstanceState State { get; private set; }

            public InstanceManager(AdKatsEngine engine) {
                try {
                    //Initial state
                    State = InstanceState.Setup;
                    //References
                    _engine = engine;
                    //Start threads
                    StartInstanceMonitor();
                }
                catch (Exception e) {
                    _engine.Log.Exception("Error while starting Instance Manager. " + _engine.GetType().Name + " in exception state.", e);
                    State = InstanceState.Exception;
                }
            }

            public void OnProconEnabled(Boolean pEnabled) {
                ProconEnabled = pEnabled;
                _instanceMonitorWaitHandle.Set();
            }

            private void SetProconEnable(Boolean enable) {
                if (enable && !ProconEnabled) {
                    _engine.ExecuteCommand("procon.protected.plugins.enable", _engine.GetType().Name, "True");
                } else if (!enable && ProconEnabled) {
                    _engine.ExecuteCommand("procon.protected.plugins.enable", _engine.GetType().Name, "False");
                }
            }

            private void SetTargetState(InstanceState state) {
                SetCurrentState(state);
                _instanceMonitorWaitHandle.Set();
            }

            private void SetCurrentState(InstanceState state) {
                State = state;
            }

            private void StartInstanceMonitor() {
                //Instance monitor
                _instanceMonitorWaitHandle.Reset();
                _engine.Exe.Async("InstanceMonitorMain", () => {
                    //Run permanently
                    while (true) {
                        try {
                            //Check state
                            switch (State) {
                                case InstanceState.Setup:
                                    RunSetupSequence();
                                    break;
                                case InstanceState.Stopped:
                                    //If stopped, and procon enabled, run startup sequence
                                    if (ProconEnabled) {
                                        RunStartupSequence();
                                    }
                                    break;
                                case InstanceState.Starting:
                                    //If starting, and procon disabled, send acknowledgement
                                    if (!ProconEnabled) {
                                        _engine.Log.Info("Shutdown requested during startup. Please wait.");
                                    }
                                    break;
                                case InstanceState.Running:
                                    //If running, and procon disabled, run shutdown sequence
                                    if (!ProconEnabled) {
                                        RunShutdownSequence();
                                    }
                                    break;
                                case InstanceState.Stopping:
                                    //If stopping, and procon enabled, send acknowledgement
                                    if (ProconEnabled) {
                                        _engine.Log.Info("Startup will commence after shutdown is complete. Please wait.");
                                    }
                                    break;
                                case InstanceState.Exception:
                                    //If in exception state, send acklowledgement, cancel actions
                                    _engine.Log.Error("Instance in exception state and cannot function. Inform ColColonCleaner, this should not happen.");
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            //Wait for input
                            _instanceMonitorWaitHandle.Reset();
                            _instanceMonitorWaitHandle.WaitOne(Timeout.Infinite);
                        }
                        catch (Exception e) {
                            _engine.Log.Exception("Error in instance monitor loop. Skipping.", e);
                            Thread.Sleep(TimeSpan.FromSeconds(10));
                        }
                    }
                }, false);
            }

            private void RunSetupSequence() {
                _engine.Log.Info("Preparing for startup.");
                SetCurrentState(InstanceState.Setup);
                _engine.Exe.Async("EngineSetup", () => {
                    //Wait to represent registered setup code
                    Thread.Sleep(5000);
                    //For longer running code, kick at least every 30 seconds
                    _engine.Exe.KickWatchdog();
                    _engine.Log.Info(_engine.GetType().Name + " " + _engine.GetPluginVersion() + " ready!");
                    SetTargetState(InstanceState.Stopped);
                    return true;
                });
            }

            private void RunStartupSequence() {
                _engine.Log.Info("Starting up.");
                SetCurrentState(InstanceState.Starting);
                _engine.Exe.Async("EngineStartup", () => {
                    //Wait to represent startup code
                    Thread.Sleep(5000);
                    //For longer running code, kick at least every 30 seconds
                    _engine.Exe.KickWatchdog();
                    if (!ProconEnabled) {
                        _engine.Log.Info("Startup cancelled.");
                        RunShutdownSequence();
                        return true;
                    }
                    _engine.Log.Info(_engine.GetType().Name + " " + _engine.GetPluginVersion() + " running!");
                    SetTargetState(InstanceState.Running);
                    return true;
                });
            }

            private void RunShutdownSequence() {
                _engine.Log.Info("Shutting down.");
                SetCurrentState(InstanceState.Stopping);
                _engine.Exe.Async("EngineShutdown", () => {
                    //Wait to represent shutdown code
                    Thread.Sleep(5000);
                    //For longer running code, kick at least every 30 seconds
                    _engine.Exe.KickWatchdog();
                    _engine.Log.Info(_engine.GetType().Name + " " + _engine.GetPluginVersion() + " stopped!");
                    SetTargetState(InstanceState.Stopped);
                    return true;
                });
            }
        }

        public class LogManager {
            //Plugin reference
            private readonly AdKatsEngine _engine;

            public LogManager(AdKatsEngine engine) {
                _engine = engine;
            }

            public enum DebugLevel {
                None,
                Basic,
                Detail,
                Trace,
                SuperTrace
            }

            public DebugLevel CurrentDebugLevel { get; set; }

            private void WriteConsole(String msg) {
                _engine.ExecuteCommand("procon.protected.pluginconsole.write", "[^b" + _engine.GetType().Name + "^n] " + msg);
            }

            private void WriteChat(String msg) {
                _engine.ExecuteCommand("procon.protected.chat.write", _engine.GetType().Name + " > " + msg);
            }

            public void ProconEventEnter() {
                Debug("Entering event " + Helper.AlphaNum(new StackFrame(1).GetMethod().Name) + " on " + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Helper.AlphaNum(Thread.CurrentThread.Name))) + Thread.CurrentThread.ManagedThreadId, DebugLevel.Trace);
            }

            public void ProconEventExit() {
                Debug("Exiting event " + Helper.AlphaNum(new StackFrame(1).GetMethod().Name) + " on " + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Helper.AlphaNum(Thread.CurrentThread.Name))) + Thread.CurrentThread.ManagedThreadId, DebugLevel.Trace);
            }

            public void Debug(String msg, DebugLevel level) {
                if (CurrentDebugLevel >= level) {
                    if (CurrentDebugLevel >= DebugLevel.SuperTrace) {
                        WriteConsole("[" + level + "-" + Helper.AlphaNum(new StackFrame(1).GetMethod().Name) + "-" + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Helper.AlphaNum(Thread.CurrentThread.Name))) + Thread.CurrentThread.ManagedThreadId + "] " + msg);
                    }
                    else {
                        WriteConsole(msg);
                    }
                }
            }

            public void Write(String msg) {
                WriteConsole(msg);
            }

            public void Info(String msg) {
                WriteConsole("^b^0INFO^n^0: " + msg);
            }

            public void Warn(String msg) {
                WriteConsole("^b^3WARNING^n^0: " + msg);
            }

            public void Error(String msg) {
                if (CurrentDebugLevel >= DebugLevel.SuperTrace) {
                    WriteConsole("^b^1ERROR-" + Int32.Parse(_engine.GetPluginVersion().Replace(".", "")) + "-" + Helper.AlphaNum(new StackFrame(1).GetMethod().Name) + "-" + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Helper.AlphaNum(Thread.CurrentThread.Name))) + Thread.CurrentThread.ManagedThreadId + "^n^0: " + "[" + msg + "]");
                }
                else {
                    WriteConsole("^b^1ERROR-" + Int32.Parse(_engine.GetPluginVersion().Replace(".", "")) + "^n^0: " + "[" + msg + "]");
                }
            }

            public void Success(String msg) {
                WriteConsole("^b^2SUCCESS^n^0: " + msg);
            }

            public void Exception(String msg, Exception e) {
                string exceptionMessage = "^b^8EXCEPTION-" + Int32.Parse(_engine.GetPluginVersion().Replace(".", ""));
                if (e != null) {
                    exceptionMessage += "-";
                    Int64 impericalLineNumber = 0;
                    Int64 parsedLineNumber = 0;
                    StackTrace stack = new StackTrace(e, true);
                    if (stack.FrameCount > 0) {
                        impericalLineNumber = stack.GetFrame(0).GetFileLineNumber();
                    }
                    Int64.TryParse(e.ToString().Split(' ').Last(), out parsedLineNumber);
                    if (impericalLineNumber != 0) {
                        exceptionMessage += impericalLineNumber;
                    }
                    else if (parsedLineNumber != 0) {
                        exceptionMessage += parsedLineNumber;
                    }
                    else {
                        exceptionMessage += "D";
                    }
                }
                exceptionMessage += "-" + Helper.AlphaNum(new StackFrame(1).GetMethod().Name) + "-" + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Helper.AlphaNum(Thread.CurrentThread.Name))) + Thread.CurrentThread.ManagedThreadId + "^n^0: " + "[" + msg + "]" + ((e != null) ? ("[" + e + "]") : (""));
                WriteConsole(exceptionMessage);
            }

            public void Chat(String msg) {
                msg = msg.Replace(Environment.NewLine, String.Empty);
                WriteChat(msg);
            }

            public String FBold(String msg) {
                return "^b" + msg + "^n";
            }

            public String FItalic(String msg) {
                return "^i" + msg + "^n";
            }

            public String CMaroon(String msg) {
                return "^1" + msg + "^0";
            }

            public String CGreen(String msg) {
                return "^2" + msg + "^0";
            }

            public String COrange(String msg) {
                return "^3" + msg + "^0";
            }

            public String CBlue(String msg) {
                return "^4" + msg + "^0";
            }

            public String CBlueLight(String msg) {
                return "^5" + msg + "^0";
            }

            public String CViolet(String msg) {
                return "^6" + msg + "^0";
            }

            public String CPink(String msg) {
                return "^7" + msg + "^0";
            }

            public String CRed(String msg) {
                return "^8" + msg + "^0";
            }

            public String CGrey(String msg) {
                return "^9" + msg + "^0";
            }
        }

        public class Helper {
            private static readonly Regex AlphaNumRegex = new Regex("[^a-zA-Z0-9]");

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string GetCurrentMethod() {
                return new StackTrace().GetFrame(1).GetMethod().Name;
            }

            public static string AlphaNum(String msg) {
                return AlphaNumRegex.Replace(msg, "");
            }

            public static String GetTimeString(TimeSpan duration, Int32 maxComponents) {
                String timeString = null;
                if (maxComponents < 1) {
                    return null;
                }
                String formattedTime = (duration.TotalMilliseconds >= 0) ? ("") : ("-");

                Double secondSubset = Math.Abs(duration.TotalSeconds);
                if (secondSubset < 1) {
                    return "0s";
                }
                Double minuteSubset = (secondSubset / 60);
                Double hourSubset = (minuteSubset / 60);
                Double daySubset = (hourSubset / 24);
                Double weekSubset = (daySubset / 7);
                Double monthSubset = (weekSubset / 4);
                Double yearSubset = (monthSubset / 12);

                int years = (Int32) yearSubset;
                Int32 months = (Int32) monthSubset % 12;
                Int32 weeks = (Int32) weekSubset % 4;
                Int32 days = (Int32) daySubset % 7;
                Int32 hours = (Int32) hourSubset % 24;
                Int32 minutes = (Int32) minuteSubset % 60;
                Int32 seconds = (Int32) secondSubset % 60;

                Int32 usedComponents = 0;
                if (years > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += years + "y";
                }
                if (months > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += months + "M";
                }
                if (weeks > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += weeks + "w";
                }
                if (days > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += days + "d";
                }
                if (hours > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += hours + "h";
                }
                if (minutes > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += minutes + "m";
                }
                if (seconds > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += seconds + "s";
                }
                timeString = formattedTime;
                if (String.IsNullOrEmpty(timeString)) {
                    timeString = "0s";
                }
                return timeString;
            }

            public static string GetTimeMs(TimeSpan duration) {
                return Math.Round(duration.TotalMilliseconds, 1) + "ms";
            }

            public static string GetTimeS(TimeSpan duration) {
                return Math.Round(duration.TotalSeconds, 1) + "s";
            }
        }

        #region Unused functions

        public string GetPluginDescription() {
            //TODO: Return static or generated engine description
            return null;
        }

        public List<CPluginVariable> GetDisplayPluginVariables() {
            //TODO: Return visible engine settings
            return null;
        }

        public List<CPluginVariable> GetPluginVariables() {
            //TODO: Return saved engine settings
            return null;
        }

        public void SetPluginVariable(string strVariable, string strValue) {
            //TODO: Accept changes to engine settings
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion) {
            //TODO: Import hostname and port
            //TODO: Import procon version
            //Register to all procon events
            RegisterEvents(GetType().Name, "OnLogin", "OnVersion", "OnServerInfo", "OnResponseError", "OnYelling", "OnSaying", "OnSupportedMaps", "OnListPlaylists", "OnListPlayers", "OnEndRound", "OnRunNextLevel", "OnCurrentLevel", "OnSpectatorListLoad", "OnSpectatorListSave", "OnSpectatorListPlayerAdded", "OnSpectatorListPlayerRemoved", "OnSpectatorListCleared", "OnSpectatorListList", "OnFairFight", "OnIsHitIndicator", "OnCommander", "OnAlwaysAllowSpectators", "OnForceReloadWholeMags", "OnServerType", "OnMaxSpectators", "OnBanAdded", "OnBanRemoved", "OnBanListClear", "OnBanListSave", "OnBanListLoad", "OnBanList", "OnMaplistConfigFile", "OnMaplistLoad", "OnMaplistSave", "OnMaplistList", "OnMaplistCleared", "OnMaplistMapAppended", "OnMaplistNextLevelIndex", "OnMaplistGetMapIndices", "OnMaplistGetRounds", "OnMaplistMapRemoved", "OnMaplistMapInserted", "OnPlayerIdleDuration", "OnPlayerIsAlive", "OnPlayerPingedByAdmin", "OnSquadLeader", "OnSquadListActive", "OnSquadListPlayers", "OnSquadIsPrivate", "OnServerName", "OnServerDescription", "OnServerMessage", "OnGamePassword", "OnPunkbuster", "OnRanked", "OnRankLimit", "OnPlayerLimit", "OnMaxPlayerLimit", "OnMaxPlayers", "OnCurrentPlayerLimit", "OnIdleTimeout", "OnIdleBanRounds", "OnProfanityFilter", "OnRoundRestartPlayerCount", "OnRoundStartPlayerCount", "OnGameModeCounter", "OnCtfRoundTimeModifier", "OnRoundTimeLimit", "OnTicketBleedRate", "OnRoundLockdownCountdown", "OnRoundWarmupTimeout", "OnPremiumStatus", "OnGunMasterWeaponsPreset", "OnVehicleSpawnAllowed", "OnVehicleSpawnDelay", "OnBulletDamage", "OnOnlySquadLeaderSpawn", "OnSoldierHealth", "OnPlayerManDownTime", "OnPlayerRespawnTime", "OnHud", "OnNameTag", "OnFriendlyFire", "OnHardcore", "OnUnlockMode", "OnPreset", "OnTeamKillCountForKick", "OnTeamKillValueIncrease", "OnTeamKillValueDecreasePerSecond", "OnTeamKillValueForKick", "OnReservedSlotsConfigFile", "OnReservedSlotsLoad", "OnReservedSlotsSave", "OnReservedSlotsPlayerAdded", "OnReservedSlotsPlayerRemoved", "OnReservedSlotsCleared", "OnReservedSlotsList", "OnReservedSlotsListAggressiveJoin", "OnPlayerKilledByAdmin", "OnPlayerKickedByAdmin", "OnPlayerMovedByAdmin", "OnTeamFactionOverride", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerDisconnected", "OnPlayerAuthenticated", "OnPlayerKilled", "OnPlayerKicked", "OnPlayerSpawned", "OnPlayerTeamChange", "OnPlayerSquadChange", "OnRoundOverPlayers", "OnRoundOverTeamScores", "OnRoundOver", "OnLevelLoaded", "OnPunkbusterPlayerInfo", "OnAccountCreated", "OnAccountDeleted", "OnAccountPrivilegesUpdate", "OnAccountLogin", "OnAccountLogout");
        }

        public void OnPluginLoadingEnv(List<String> lstPluginEnv) {
            //TODO: Process assembly version
            //TODO: Process game type
            //TODO: Process game mod (if applicable)
            //TODO: Process sandbox enabled, disallow engine enable in sandbox enabled
        }

        public override void OnLogin() {
            //TODO: Handle reconnect to battlefield server, reset all possibly corrupt data
        }

        public override void OnVersion(string serverType, string version) {
            //TODO: Handle server type and version (already known)
        }

        public override void OnServerInfo(CServerInfo serverInfo) {
            //TODO: Handle updated server info
        }

        public override void OnResponseError(List<string> requestWords, string error) {
            //TODO: Handle error returned after server command
        }

        public override void OnYelling(string message, int messageDuration, CPlayerSubset subset) {
            //TODO: Handle successful yells
        }

        public override void OnSaying(string message, CPlayerSubset subset) {
            //TODO: Handle successful says
        }

        public override void OnRestartLevel() {
            //TODO: Handle level restart
        }

        public override void OnSupportedMaps(string playlist, List<string> lstSupportedMaps) {
            //TODO: Handle list of supported maps
        }

        public override void OnListPlaylists(List<string> playlists) {
            //TODO: Find out what this is
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
            //TODO: Handle updated player info
        }

        public override void OnEndRound(int iWinningTeamID) {
            //TODO: Combine with other 2 end events
        }

        public override void OnRunNextLevel() {
            //TODO: Handle run next round event
        }

        public override void OnCurrentLevel(string mapFileName) {
            //TODO: Handle current level name
        }

        public override void OnSpectatorListLoad() {
            //TODO: Handle spectator list
        }

        public override void OnSpectatorListSave() {
            //TODO: Handle spectator list
        }

        public override void OnSpectatorListPlayerAdded(string soldierName) {
            //TODO: Handle spectator list
        }

        public override void OnSpectatorListPlayerRemoved(string soldierName) {
            //TODO: Handle spectator list
        }

        public override void OnSpectatorListCleared() {
            //TODO: Handle spectator list
        }

        public override void OnSpectatorListList(List<string> soldierNames) {
            //TODO: Handle spectator list
        }

        public override void OnFairFight(bool isEnabled) {
            //TODO: Handle fairfight enable
        }

        public override void OnIsHitIndicator(bool isEnabled) {
            //TODO: Handle hit indicator enabled
        }

        public override void OnCommander(bool isEnabled) {
            //TODO: Handle commander enabled
        }

        public override void OnAlwaysAllowSpectators(bool isEnabled) {
            //TODO: Handle global spectator allowance
        }

        public override void OnForceReloadWholeMags(bool isEnabled) {
            //TODO: Handle force reload whole mags
        }

        public override void OnServerType(string value) {
            //TODO: Handle server type (known?)
        }

        public override void OnMaxSpectators(int spectatorLimit) {
            //TODO: Handle max spectators
        }

        public override void OnBanAdded(CBanInfo ban) {
            //TODO: Handle ban update
        }

        public override void OnBanRemoved(CBanInfo ban) {
            //TODO: Handle ban update
        }

        public override void OnBanListClear() {
            //TODO: Handle ban update
        }

        public override void OnBanListSave() {
            //TODO: Handle ban update
        }

        public override void OnBanListLoad() {
            //TODO: Handle ban update
        }

        public override void OnBanList(List<CBanInfo> banList) {
            //TODO: Handle ban update
        }

        public override void OnMaplistConfigFile(string configFileName) {
            //TODO: Handle config file?
        }

        public override void OnMaplistLoad() {
            //TODO: Handle map list updates
        }

        public override void OnMaplistSave() {
            //TODO: Handle map list updates
        }

        public override void OnMaplistList(List<MaplistEntry> lstMaplist) {
            //TODO: Handle map list updates
        }

        public override void OnMaplistCleared() {
            //TODO: Handle map list updates
        }

        public override void OnMaplistMapAppended(string mapFileName) {
            //TODO: Handle map list updates
        }

        public override void OnMaplistNextLevelIndex(int mapIndex) {
            //TODO: Handle map list updates
        }

        public override void OnMaplistGetMapIndices(int mapIndex, int nextIndex) {
            //TODO: Handle map list updates
        }

        public override void OnMaplistGetRounds(int currentRound, int totalRounds) {
            //TODO: Handle map list updates
        }

        public override void OnMaplistMapRemoved(int mapIndex) {
            //TODO: Handle map list updates
        }

        public override void OnMaplistMapInserted(int mapIndex, string mapFileName) {
            //TODO: Handle map list updates
        }

        public override void OnPlayerIdleDuration(string soldierName, int idleTime) {
            //TODO: Handle player idle duration
        }

        public override void OnPlayerIsAlive(string soldierName, bool isAlive) {
            //TODO: Handle player alive state
        }

        public override void OnPlayerPingedByAdmin(string soldierName, int ping) {
            //TODO: Handle player ping update
        }

        public override void OnSquadLeader(int teamId, int squadId, string soldierName) {
            //TODO: Handle player leader status
        }

        public override void OnSquadListActive(int teamId, int squadCount, List<int> squadList) {
            //TODO: Find out what this is
        }

        public override void OnSquadListPlayers(int teamId, int squadId, int playerCount, List<string> playersInSquad) {
            //TODO: Handle squad list updates
        }

        public override void OnSquadIsPrivate(int teamId, int squadId, bool isPrivate) {
            //TODO: Handle squad private status
        }

        public override void OnServerName(string serverName) {
            //TODO: Handle updated server name
        }

        public override void OnServerDescription(string serverDescription) {
            //TODO: Handle updated server description
        }

        public override void OnServerMessage(string serverMessage) {
            //TODO: Find out what this is
        }

        //Config 


        public override void OnGamePassword(string gamePassword) {
        }

        public override void OnPunkbuster(bool isEnabled) {
        }

        public override void OnRanked(bool isEnabled) {
        }

        public override void OnPlayerLimit(int limit) {
        }

        public override void OnMaxPlayerLimit(int limit) {
        }

        public override void OnMaxPlayers(int limit) {
        }

        public override void OnCurrentPlayerLimit(int limit) {
        }

        public override void OnIdleTimeout(int limit) {
        }

        public override void OnIdleBanRounds(int limit) {
        }

        public override void OnRoundRestartPlayerCount(int limit) {
        }

        public override void OnRoundStartPlayerCount(int limit) {
        }

        public override void OnGameModeCounter(int limit) {
        }

        public override void OnCtfRoundTimeModifier(int limit) {
        }

        public override void OnRoundTimeLimit(int limit) {
        }

        public override void OnTicketBleedRate(int limit) {
        }

        public override void OnRoundLockdownCountdown(int limit) {
        }

        public override void OnRoundWarmupTimeout(int limit) {
        }

        public override void OnPremiumStatus(bool isEnabled) {
        }

        public override void OnGunMasterWeaponsPreset(int preset) {
        }

        public override void OnVehicleSpawnAllowed(bool isEnabled) {
        }

        public override void OnVehicleSpawnDelay(int limit) {
        }

        public override void OnBulletDamage(int limit) {
        }

        public override void OnOnlySquadLeaderSpawn(bool isEnabled) {
        }

        public override void OnSoldierHealth(int limit) {
        }

        public override void OnPlayerManDownTime(int limit) {
        }

        public override void OnPlayerRespawnTime(int limit) {
        }

        public override void OnNameTag(bool isEnabled) {
        }

        public override void OnFriendlyFire(bool isEnabled) {
        }

        public override void OnHardcore(bool isEnabled) {
        }

        public override void OnUnlockMode(string mode) {
        }

        public override void OnPreset(string mode, bool isLocked) {
        }

        public override void OnTeamKillCountForKick(int limit) {
        }

        public override void OnTeamKillValueIncrease(int limit) {
        }

        public override void OnTeamKillValueDecreasePerSecond(int limit) {
        }

        public override void OnTeamKillValueForKick(int limit) {
        }

        public override void OnReservedSlotsConfigFile(string configFileName) {
        }

        public override void OnReservedSlotsLoad() {
        }

        public override void OnReservedSlotsSave() {
        }

        public override void OnReservedSlotsPlayerAdded(string soldierName) {
        }

        public override void OnReservedSlotsPlayerRemoved(string soldierName) {
        }

        public override void OnReservedSlotsCleared() {
        }

        public override void OnReservedSlotsList(List<string> soldierNames) {
        }

        public override void OnReservedSlotsListAggressiveJoin(bool isEnabled) {
        }

        public override void OnPlayerKilledByAdmin(string soldierName) {
        }

        public override void OnPlayerKickedByAdmin(string soldierName, string reason) {
        }

        public override void OnPlayerMovedByAdmin(string soldierName, int destinationTeamId, int destinationSquadId, bool forceKilled) {
        }


        public override void OnTeamFactionOverride(int teamId, int faction) {
        }

        public override void OnPlayerJoin(string soldierName) {
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo) {
        }

        public override void OnPlayerDisconnected(string soldierName, string reason) {
        }

        public override void OnPlayerAuthenticated(string soldierName, string guid) {
        }

        public override void OnPlayerKilled(Kill kKillerVictimDetails) {
        }

        public override void OnPlayerKicked(string soldierName, string reason) {
        }

        public override void OnPlayerSpawned(string soldierName, Inventory spawnedInventory) {
        }

        public override void OnPlayerTeamChange(string soldierName, int teamId, int squadId) {
        }

        public override void OnPlayerSquadChange(string soldierName, int teamId, int squadId) {
        }


        public override void OnRoundOverPlayers(List<CPlayerInfo> players) {
        }

        public override void OnRoundOverTeamScores(List<TeamScore> teamScores) {
        }

        public override void OnRoundOver(int winningTeamId) {
        }

        public override void OnLevelLoaded(string mapFileName, string gamemode, int roundsPlayed, int roundsTotal) {
        } // BF3


        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo playerInfo) {
        }

        public override void OnAccountCreated(string username) {
        }

        public override void OnAccountDeleted(string username) {
        }

        public override void OnAccountPrivilegesUpdate(string username, CPrivileges privileges) {
        }

        public override void OnAccountLogin(string accountName, string ip, CPrivileges privileges) {
        }

        public override void OnAccountLogout(string accountName, string ip, CPrivileges privileges) {
        }

        #endregion
    }
}