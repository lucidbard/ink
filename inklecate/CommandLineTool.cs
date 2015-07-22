﻿using System;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace Inklewriter
{
	class CommandLineTool
	{
		class Options {
			public bool testMode;
            public bool stressTest;
            public bool verbose;
			public bool playMode;
			public string inputFile;
            public string outputFile;
            public bool indentedJson;
		}

		public static int ExitCodeError = 1;

		public static void Main (string[] args)
		{
			new CommandLineTool(args);
		}

        void ExitWithUsageInstructions()
        {
            Console.WriteLine (
                "Usage: inklecate2 <options> <ink file> \n"+
                "   -o <filename>:  Output file name\n"+
                "   -p:             Play mode\n"+
                "   -i:             Use indentation in output JSON\n"+
                "   -v:             Verbose mode - print compilation timings\n"+
                "   -t:             Test mode - loads up test.ink\n"+
                "   -s:             Stress test mode - generates test content and \n" +
                "                   times compilation\n");
            Environment.Exit (ExitCodeError);
        }
            
		CommandLineTool(string[] args)
		{
            if (ProcessArguments (args) == false) {
                ExitWithUsageInstructions ();
            }

            if (opts.testMode) {
                opts.inputFile = "test.ink";
            }

            if (opts.inputFile == null) {
                ExitWithUsageInstructions ();
            }

            if (opts.outputFile == null) {
                opts.outputFile = Path.ChangeExtension (opts.inputFile, ".ink.json");
            }

            string inputString = null;

            if (opts.stressTest) {

                StressTestContentGenerator stressTestContent = null;
                TimeOperation ("Generating test content", () => {
                    stressTestContent = new StressTestContentGenerator (100);
                });

                Console.WriteLine ("Generated ~{0}k of test ink", stressTestContent.sizeInKiloChars);

                inputString = stressTestContent.content;

            } else {
                try {
                    inputString = File.ReadAllText(opts.inputFile);
                }
                catch {
                    Console.WriteLine ("Could not open file '" + opts.inputFile+"'");
                    Environment.Exit (ExitCodeError);
                }
            }

            InkParser parser = null;
            Parsed.Story parsedStory = null;
            Runtime.Story story = null;

            TimeOperation ("Creating parser", () => {
                parser = new InkParser (inputString, opts.inputFile);
            });

            TimeOperation ("Parsing", () => {
                parsedStory = parser.Parse();
            });

            if (parsedStory == null) {
                Environment.Exit (ExitCodeError);
            }

            TimeOperation ("Exporting runtime", () => {
                story = parsedStory.ExportRuntime ();
            });

			if (story == null) {
				Environment.Exit (ExitCodeError);
			}
                
            // JSON round trip testing
//            if (opts.testMode) {
//                var jsonStr = story.ToJsonString (indented:true);
//                Console.WriteLine (jsonStr);
//
//                Console.WriteLine ("---------------------------------------------------");
//
//                var reloadedStory = new Runtime.Story (jsonStr);
//                var newJsonStr = reloadedStory.ToJsonString (indented: true);
//                Console.WriteLine (newJsonStr);
//
//                story = reloadedStory;
//            }

			// Play mode
            // Test mode may use "-tp" in commmand line args to specify that
            // the test script is also played
            if (opts.playMode) {

                var player = new CommandLinePlayer (story, false, parsedStory);
                player.Begin ();
            } 

            // Compile mode
            else {
                
                var jsonStr = story.ToJsonString (opts.indentedJson);

                try {
                    File.WriteAllText (opts.outputFile, jsonStr, System.Text.Encoding.UTF8);
                } catch {
                    Console.WriteLine ("Could write to output file '" + opts.outputFile+"'");
                    Environment.Exit (ExitCodeError);
                }
            }
		}

        bool ProcessArguments(string[] args)
		{
            if (args.Length < 1) {
                opts = null;
                return false;
            }

			opts = new Options();

            bool nextArgIsOutputFilename = false;

			// Process arguments
            int argIdx = 0;
			foreach (string arg in args) {
                            
                if (nextArgIsOutputFilename) {
                    opts.outputFile = arg;
                    nextArgIsOutputFilename = false;
                }

				// Options
				var firstChar = arg.Substring(0,1);
                if (firstChar == "-" && arg.Length > 1) {

                    for (int i = 1; i < arg.Length; ++i) {
                        char argChar = arg [i];

                        switch (argChar) {
                        case 't':
                            opts.testMode = true;
                            break;
                        case 's':
                            opts.testMode = true;
                            opts.stressTest = true;
                            opts.verbose = true;
                            break;
                        case 'p':
                            opts.playMode = true;
                            break;
                        case 'v':
                            opts.verbose = true;
                            break;
                        case 'o':
                            nextArgIsOutputFilename = true;   
                            break;
                        case 'i':
                            opts.indentedJson = true;
                            break;
                        default:
                            Console.WriteLine ("Unsupported argument type: '{0}'", argChar);
                            break;
                        }
                    }
                } 
                    
                // Last argument: input file
                else if( argIdx == args.Length-1 ) {
                    opts.inputFile = arg;
                }

                argIdx++;
			}

			return true;
		}

        void TimeOperation(string opDescription, Action op)
        {
            if (!opts.verbose) {
                op ();
                return;
            }

            Console.WriteLine ("{0}...", opDescription);

            var stopwatch = Stopwatch.StartNew ();
            op ();
            stopwatch.Stop ();

            long duration = stopwatch.ElapsedMilliseconds;

            if (duration > 500) {
                Console.WriteLine ("{0} took {1}s", opDescription, duration / 1000.0f);  
            } else {
                Console.WriteLine ("{0} took {1}ms", opDescription, duration);  
            }
        }

        Options opts;
	}
}