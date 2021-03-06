﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Ink
{
	internal partial class InkParser : StringParser
	{
        public InkParser(string str, string filenameForMetadata = null, Ink.ErrorHandler externalErrorHandler = null) 
            : this(str, filenameForMetadata, externalErrorHandler, null) 
        {  }

        InkParser(string str, string inkFilename = null, Ink.ErrorHandler externalErrorHandler = null, InkParser rootParser = null) : base(str) { 
            _filename = inkFilename;
			RegisterExpressionOperators ();
            GenerateStatementLevelRules ();
            this.errorHandler = OnError;
            _externalErrorHandler = externalErrorHandler;

            if (rootParser == null) {
                _rootParser = this;

                _openFilenames = new HashSet<string> ();

                if (inkFilename != null) {

                    var workingDir = Directory.GetCurrentDirectory();
                    var fullRootInkPath = Path.Combine (workingDir, inkFilename);
                    
                    _openFilenames.Add (fullRootInkPath);
                }

            } else {
                _rootParser = rootParser;
            }

		}

        // Main entry point
        public Parsed.Story Parse()
        {
            List<Parsed.Object> topLevelContent = StatementsAtLevel (StatementLevel.Top);
            if (hadError) {
                return null;
            }

            return new Parsed.Story (topLevelContent);
        }

        protected override string PreProcessInputString(string str)
        {
            var inputWithCommentsRemoved = (new CommentEliminator (str)).Process();
            return inputWithCommentsRemoved;
        }

        protected override void RuleDidSucceed(object result, StringParserState.Element stateAtStart, StringParserState.Element stateAtEnd)
        {
            // Apply DebugMetadata based on the state at the start of the rule
            // (i.e. use line number as it was at the start of the rule)
            var parsedObj = result as Parsed.Object;
            if ( parsedObj) {
                var md = new Runtime.DebugMetadata ();
                md.startLineNumber = stateAtStart.lineIndex + 1;
                md.endLineNumber = stateAtEnd.lineIndex + 1;
                md.fileName = _filename;
                parsedObj.debugMetadata = md;
                return;
            }

            // A list of objects that doesn't already have metadata?
            var parsedListObjs = result as List<Parsed.Object>;
            if (parsedListObjs != null) {
                foreach (var parsedListObj in parsedListObjs) {
                    if (!parsedListObj.hasOwnDebugMetadata) {
                        var md = new Runtime.DebugMetadata ();
                        md.startLineNumber = stateAtStart.lineIndex + 1;
                        md.endLineNumber = stateAtEnd.lineIndex + 1;
                        md.fileName = _filename;
                        parsedListObj.debugMetadata = md;
                    }
                }
            }
        }
            
        protected bool parsingStringExpression
        {
            get {
                return GetFlag ((uint)CustomFlags.ParsingString);
            } 
            set {
                SetFlag ((uint)CustomFlags.ParsingString, value);
            }
        }

        protected enum CustomFlags {
            ParsingString = 0x1
        }

        void OnError(string message, int index, int lineIndex, bool isWarning)
        {
            var warningType = isWarning ? "WARNING:" : "ERROR:";
            string fullMessage;

            if (_filename != null) {
                fullMessage = string.Format(warningType+" '{0}' line {1}: {2}",  _filename, (lineIndex+1), message);
            } else {
                fullMessage = string.Format(warningType+" line {0}: {1}", (lineIndex+1), message);
            }

            if (_externalErrorHandler != null) {
                _externalErrorHandler (fullMessage, isWarning ? ErrorType.Warning : ErrorType.Error);
            } else {
                Console.WriteLine (fullMessage);
            }
        }

        Ink.ErrorHandler _externalErrorHandler;

        string _filename;
	}
}

