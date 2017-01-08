using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;

namespace SInnovations.ServiceFabric.CoreCLR.Tools.FabUtil
{
    /// <summary>
    /// A class which encapsulates logic needed to forward arguments from the current process to another process
    /// invoked with the dotnet.exe host.
    /// </summary>
    public class ForwardingApp
    {
        private const string s_hostExe = "dotnet";

        private readonly string _forwardApplicationPath;
        private readonly IEnumerable<string> _argsToForward;
        private readonly string _depsFile;
        private readonly string _runtimeConfig;
        private readonly string _additionalProbingPath;
        private Dictionary<string, string> _environmentVariables;

        private readonly string[] _allArgs;

        public ForwardingApp(
            string forwardApplicationPath,
            IEnumerable<string> argsToForward,
            string depsFile = null,
            string runtimeConfig = null,
            string additionalProbingPath = null,
            Dictionary<string, string> environmentVariables = null)
        {
            _forwardApplicationPath = forwardApplicationPath;
            _argsToForward = argsToForward;
            _depsFile = depsFile;
            _runtimeConfig = runtimeConfig;
            _additionalProbingPath = additionalProbingPath;
            _environmentVariables = environmentVariables;

            var allArgs = new List<string>();
            allArgs.Add("exec");

            if (_depsFile != null)
            {
                allArgs.Add("--depsfile");
                allArgs.Add(_depsFile);
            }

            if (_runtimeConfig != null)
            {
                allArgs.Add("--runtimeconfig");
                allArgs.Add(_runtimeConfig);
            }

            if (_additionalProbingPath != null)
            {
                allArgs.Add("--additionalprobingpath");
                allArgs.Add(_additionalProbingPath);
            }

            allArgs.Add(_forwardApplicationPath);
            allArgs.AddRange(_argsToForward);

            _allArgs = allArgs.ToArray();
        }

        public int Execute()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = GetHostExeName(),
                Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(_allArgs),
                UseShellExecute = false
            };

            if (_environmentVariables != null)
            {
                foreach (var entry in _environmentVariables)
                {
                    processInfo.Environment[entry.Key] = entry.Value;
                }
            }

            var process = new Process
            {
                StartInfo = processInfo
            };

            process.Start();
            process.WaitForExit();

            return process.ExitCode;
        }

        public ForwardingApp WithEnvironmentVariable(string name, string value)
        {
            _environmentVariables = _environmentVariables ?? new Dictionary<string, string>();

            _environmentVariables.Add(name, value);

            return this;
        }

        private string GetHostExeName()
        {
            return $"{s_hostExe}{FileNameSuffixes.CurrentPlatform.Exe}";
        }
    }

    public class NuGetForwardingApp
    {
        private const string s_nugetExeName = "NuGet.CommandLine.XPlat.dll";
        private readonly ForwardingApp _forwardingApp;

        public NuGetForwardingApp(IEnumerable<string> argsToForward)
        {
            _forwardingApp = new ForwardingApp(
                GetNuGetExePath(),
                argsToForward);
        }

        public int Execute()
        {
            return _forwardingApp.Execute();
        }

        public NuGetForwardingApp WithEnvironmentVariable(string name, string value)
        {
            _forwardingApp.WithEnvironmentVariable(name, value);

            return this;
        }

        private static string GetNuGetExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                s_nugetExeName);
        }
    }

    public class Program
    {
        [MTAThread]
        public static void Main(string[] args)
        {

           

            Console.WriteLine("Hello World");
            
            Console.WriteLine(Directory.GetCurrentDirectory());

            var nugetApp = new NuGetForwardingApp(new[] { "locals", "all" });

            Console.WriteLine(nugetApp.Execute());

            //C:\Users\pks\.nuget\packages\.tools\S-Innovations.ServiceFabric.CoreCLR.Tools.FabUtil
            // Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            // var fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".nuget\packages\Microsoft.ServiceFabric.Actors\2.4.145\build\FabActUtil.exe");
            // Console.WriteLine(fileName);


            //// Fires up a new process to run inside this one
            // var process = Process.Start(new ProcessStartInfo
            // {
            //     UseShellExecute = false,

            //     RedirectStandardError = true,
            //     RedirectStandardInput = true,
            //     RedirectStandardOutput = true,

            //     FileName = fileName
            // });

            // // Depending on your application you may either prioritize the IO or the exact opposite

            // var outputThread = new Thread(outputReader) { Name = "ChildIO Output"};
            // var errorThread = new Thread(errorReader) { Name = "ChildIO Error" };
            // var inputThread = new Thread(inputReader) { Name = "ChildIO Input" };

            // // Set as background threads (will automatically stop when application ends)
            // outputThread.IsBackground = errorThread.IsBackground
            //     = inputThread.IsBackground = true;

            // // Start the IO threads
            // outputThread.Start(process);
            // errorThread.Start(process);
            // inputThread.Start(process);

            // // Demonstrate that the host app can be written to by the application
            // process.StandardInput.WriteLine("Message from host");

            // // Signal to end the application
            // ManualResetEvent stopApp = new ManualResetEvent(false);

            // // Enables the exited event and set the stopApp signal on exited
            // process.EnableRaisingEvents = true;
            // process.Exited += (e, sender) => { stopApp.Set(); };

            // // Wait for the child app to stop
            // stopApp.WaitOne();

            // // Write some nice output for now?
            // Console.WriteLine();
            // Console.Write("Process ended... shutting down host");
            // Thread.Sleep(1000);

        }

        /// <summary>
        /// Continuously copies data from one stream to the other.
        /// </summary>
        /// <param name="instream">The input stream.</param>
        /// <param name="outstream">The output stream.</param>
        private static void passThrough(Stream instream, Stream outstream)
        {
            byte[] buffer = new byte[4096];
            while (true)
            {
                int len;
                while ((len = instream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outstream.Write(buffer, 0, len);
                    outstream.Flush();
                }
            }
        }

        private static void outputReader(object p)
        {
            var process = (Process)p;
            // Pass the standard output of the child to our standard output
            passThrough(process.StandardOutput.BaseStream, Console.OpenStandardOutput());
        }

        private static void errorReader(object p)
        {
            var process = (Process)p;
            // Pass the standard error of the child to our standard error
            passThrough(process.StandardError.BaseStream, Console.OpenStandardError());
        }

        private static void inputReader(object p)
        {
            var process = (Process)p;
            // Pass our standard input into the standard input of the child
            passThrough(Console.OpenStandardInput(), process.StandardInput.BaseStream);
        }
    }
}

