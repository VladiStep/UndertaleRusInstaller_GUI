using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UndertaleModLib;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;
using UndertaleModLib.Scripting;
using static UndertaleRusInstallerGUI.OSMethods;

namespace UndertaleRusInstallerGUI.Views;

public partial class MainWindow
{
    private const string msgBoxTitle = "Установщик русификатора Undertale/NXTale";

    public void ScriptMessage(string message) => MessageBox(msgBoxTitle, message, "ok", "info", 1);
    public void ScriptError(string error, string title = msgBoxTitle) => MessageBox(title, error, "ok", "error", 1);
    public bool ScriptQuestion(string message)
    {
        return MessageBox(msgBoxTitle, message, "yesno", "question", 0) != 0;
    }

    public void SimpleTextOutput(string title, string label, string defaultText, bool allowMultiline)
    {
        throw new NotImplementedException();
    }
    public string SimpleTextInput(string title, string label, string defaultValue, bool allowMultiline, bool showDialog = true)
    {
        throw new NotImplementedException();
    }

    public void UpdateProgressBar(string message, string status, double currentValue, double maxValue)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void SetProgressBar(string message, string status, double currentValue, double maxValue)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void UpdateProgressValue(double currentValue)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void UpdateProgressStatus(string status)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void AddProgress(int amount)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void IncrementProgress()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void AddProgressParallel(int amount)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void IncrementProgressParallel()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public int GetProgress()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void SetProgress(int value)
    {
        throw new NotImplementedException();
    }

    public void ImportGMLString(string codeName, string gmlCode, bool doParse = true, bool checkDecompiler = false)
    {
        ImportCode(codeName, gmlCode, true, doParse, true, checkDecompiler);
    }

    public void ImportCode(string codeName, string gmlCode, bool isGML = true, bool doParse = true, bool destroyASM = true, bool checkDecompiler = false, bool throwOnError = false)
    {
        bool skipPortions = false;
        UndertaleCode code = Core.Data.Code.ByName(codeName);
        if (code is null)
        {
            code = new UndertaleCode();
            code.Name = Core.Data.Strings.MakeString(codeName);
            Core.Data.Code.Add(code);
        }
        else if (code.ParentEntry is not null)
            return;

        if (Core.Data?.GeneralInfo.BytecodeVersion > 14 && Core.Data.CodeLocals.ByName(codeName) == null)
        {
            UndertaleCodeLocals locals = new UndertaleCodeLocals();
            locals.Name = code.Name;

            UndertaleCodeLocals.LocalVar argsLocal = new UndertaleCodeLocals.LocalVar();
            argsLocal.Name = Core.Data.Strings.MakeString("arguments");
            argsLocal.Index = 0;

            locals.Locals.Add(argsLocal);

            code.LocalsCount = 1;
            Core.Data.CodeLocals.Add(locals);
        }
        if (doParse)
        {
            // This portion links code.
            if (codeName.StartsWith("gml_Script"))
            {
                // Add code to scripts section.
                if (Core.Data.Scripts.ByName(codeName.Substring(11)) == null)
                {
                    UndertaleScript scr = new UndertaleScript();
                    scr.Name = Core.Data.Strings.MakeString(codeName.Substring(11));
                    scr.Code = code;
                    Core.Data.Scripts.Add(scr);
                }
                else
                {
                    UndertaleScript scr = Core.Data.Scripts.ByName(codeName.Substring(11));
                    scr.Code = code;
                }
            }
            else if (codeName.StartsWith("gml_GlobalScript"))
            {
                // Add code to global init section.
                UndertaleGlobalInit initEntry = null;
                // This doesn't work, have to do it the hard way: UndertaleGlobalInit init_entry = Core.Data.GlobalInitScripts.ByName(scr_dup_code_name_con);
                foreach (UndertaleGlobalInit globalInit in Core.Data.GlobalInitScripts)
                {
                    if (globalInit.Code.Name.Content == codeName)
                    {
                        initEntry = globalInit;
                        break;
                    }
                }
                if (initEntry == null)
                {
                    UndertaleGlobalInit newInit = new UndertaleGlobalInit();
                    newInit.Code = code;
                    Core.Data.GlobalInitScripts.Add(newInit);
                }
                else
                {
                    UndertaleGlobalInit NewInit = initEntry;
                    NewInit.Code = code;
                }
            }
            else if (codeName.StartsWith("gml_Object"))
            {
                string afterPrefix = codeName.Substring(11);
                int underCount = 0;
                string methodNumberStr = "", methodName = "", objName = "";
                for (int i = afterPrefix.Length - 1; i >= 0; i--)
                {
                    if (afterPrefix[i] == '_')
                    {
                        underCount++;
                        if (underCount == 1)
                        {
                            methodNumberStr = afterPrefix.Substring(i + 1);
                        }
                        else if (underCount == 2)
                        {
                            objName = afterPrefix.Substring(0, i);
                            methodName = afterPrefix.Substring(i + 1, afterPrefix.Length - objName.Length - methodNumberStr.Length - 2);
                            break;
                        }
                    }
                }
                int methodNumber = 0;
                try
                {
                    methodNumber = int.Parse(methodNumberStr);
                    if (methodName == "Collision" && (methodNumber >= Core.Data.GameObjects.Count || methodNumber < 0))
                    {
                        bool doNewObj = ScriptQuestion("Object of ID " + methodNumber.ToString() + " was not found.\nAdd new object?");
                        if (doNewObj)
                        {
                            UndertaleGameObject gameObj = new UndertaleGameObject();
                            gameObj.Name = Core.Data.Strings.MakeString(SimpleTextInput("Enter object name", "Enter object name", "This is a single text line input box test.", false));
                            Core.Data.GameObjects.Add(gameObj);
                        }
                        else
                        {
                            // It *needs* to have a valid value, make the user specify one.
                            List<uint> possibleValues = new List<uint>();
                            possibleValues.Add(uint.MaxValue);
                            methodNumber = (int)ReduceCollisionValue(possibleValues);
                        }
                    }
                }
                catch
                {
                    if (afterPrefix.LastIndexOf("_Collision_") != -1)
                    {
                        string s2 = "_Collision_";
                        objName = afterPrefix.Substring(0, (afterPrefix.LastIndexOf("_Collision_")));
                        methodNumberStr = afterPrefix.Substring(afterPrefix.LastIndexOf("_Collision_") + s2.Length, afterPrefix.Length - (afterPrefix.LastIndexOf("_Collision_") + s2.Length));
                        methodName = "Collision";
                        // GMS 2.3+ use the object name for the one colliding, which is rather useful.
                        if (Core.Data.IsVersionAtLeast(2, 3))
                        {
                            if (Core.Data.GameObjects.ByName(methodNumberStr) != null)
                            {
                                for (var i = 0; i < Core.Data.GameObjects.Count; i++)
                                {
                                    if (Core.Data.GameObjects[i].Name.Content == methodNumberStr)
                                    {
                                        methodNumber = i;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                bool doNewObj = ScriptQuestion("Object " + objName + " was not found.\nAdd new object called " + objName + "?");
                                if (doNewObj)
                                {
                                    UndertaleGameObject gameObj = new UndertaleGameObject();
                                    gameObj.Name = Core.Data.Strings.MakeString(objName);
                                    Core.Data.GameObjects.Add(gameObj);
                                }
                            }
                            if (Core.Data.GameObjects.ByName(methodNumberStr) != null)
                            {
                                // It *needs* to have a valid value, make the user specify one, silly.
                                List<uint> possibleValues = new List<uint>();
                                possibleValues.Add(uint.MaxValue);
                                ReassignGUIDs(methodNumberStr, ReduceCollisionValue(possibleValues));
                            }
                        }
                        else
                        {
                            // Let's try to get this going
                            methodNumber = (int)ReduceCollisionValue(GetCollisionValueFromCodeNameGUID(codeName));
                            ReassignGUIDs(methodNumberStr, ReduceCollisionValue(GetCollisionValueFromCodeNameGUID(codeName)));
                        }
                    }
                }
                UndertaleGameObject obj = Core.Data.GameObjects.ByName(objName);
                if (obj == null)
                {
                    bool doNewObj = ScriptQuestion("Object " + objName + " was not found.\nAdd new object called " + objName + "?");
                    if (doNewObj)
                    {
                        UndertaleGameObject gameObj = new UndertaleGameObject();
                        gameObj.Name = Core.Data.Strings.MakeString(objName);
                        Core.Data.GameObjects.Add(gameObj);
                    }
                    else
                    {
                        skipPortions = true;
                    }
                }

                if (!(skipPortions))
                {
                    obj = Core.Data.GameObjects.ByName(objName);
                    int eventIdx = Convert.ToInt32(Enum.Parse(typeof(EventType), methodName));
                    bool duplicate = false;
                    try
                    {
                        foreach (UndertaleGameObject.Event evnt in obj.Events[eventIdx])
                        {
                            foreach (UndertaleGameObject.EventAction action in evnt.Actions)
                            {
                                if (action.CodeId?.Name?.Content == codeName)
                                    duplicate = true;
                            }
                        }
                    }
                    catch
                    {
                        // Something went wrong, but probably because it's trying to check something non-existent
                        // Just keep going
                    }
                    if (duplicate == false)
                    {
                        UndertalePointerList<UndertaleGameObject.Event> eventList = obj.Events[eventIdx];
                        UndertaleGameObject.EventAction action = new UndertaleGameObject.EventAction();
                        UndertaleGameObject.Event evnt = new UndertaleGameObject.Event();

                        action.ActionName = code.Name;
                        action.CodeId = code;
                        evnt.EventSubtype = (uint)methodNumber;
                        evnt.Actions.Add(action);
                        eventList.Add(evnt);
                    }
                }
            }
        }
        SafeImport(codeName, gmlCode, isGML, destroyASM, checkDecompiler, throwOnError);
    }

    public void SafeImport(string codeName, string gmlCode, bool isGML, bool destroyASM = true, bool checkDecompiler = false, bool throwOnError = false)
    {
        UndertaleCode code = Core.Data.Code.ByName(codeName);
        if (code?.ParentEntry is not null)
            return;

        try
        {
            if (isGML)
            {
                code.ReplaceGML(gmlCode, Core.Data);
            }
            else
            {
                var instructions = Assembler.Assemble(gmlCode, Core.Data);
                code.Replace(instructions);
                if (destroyASM)
                    NukeProfileGML(codeName);
            }
        }
        catch (Exception ex)
        {
            if (!checkDecompiler)
            {
                string errorText = $"Code import error at {(isGML ? "GML" : "ASM")} code \"{codeName}\":\n\n{ex.Message}";
                Console.Error.WriteLine(errorText);

                if (throwOnError)
                    throw new ScriptException("*codeImportError*");
            }
            else
            {
                code.ReplaceGML("", Core.Data);
            }
        }
    }

    public static void NukeProfileGML(string codeName)
    {
        //CLI does not have any code editing tools (yet), nor a profile Mode thus since is completely useless
    }

    public void ReassignGUIDs(string guid, uint objectIndex)
    {
        int eventIdx = Convert.ToInt32(EventType.Collision);
        for (var i = 0; i < Core.Data.GameObjects.Count; i++)
        {
            UndertaleGameObject obj = Core.Data.GameObjects[i];
            try
            {
                foreach (UndertaleGameObject.Event evnt in obj.Events[eventIdx])
                {
                    foreach (UndertaleGameObject.EventAction action in evnt.Actions)
                    {
                        if (action.CodeId.Name.Content.Contains(guid))
                        {
                            evnt.EventSubtype = objectIndex;
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore, some values can be null along the way
            }
        }
    }
    public List<uint> GetCollisionValueFromCodeNameGUID(string codeName)
    {
        int eventIdx = Convert.ToInt32(EventType.Collision);
        List<uint> possibleValues = new List<uint>();
        for (var i = 0; i < Core.Data.GameObjects.Count; i++)
        {
            UndertaleGameObject obj = Core.Data.GameObjects[i];
            try
            {
                foreach (UndertaleGameObject.Event evnt in obj.Events[eventIdx])
                {
                    foreach (UndertaleGameObject.EventAction action in evnt.Actions)
                    {
                        if (action.CodeId.Name.Content == codeName)
                        {
                            if (Core.Data.GameObjects[(int)evnt.EventSubtype] != null)
                            {
                                possibleValues.Add(evnt.EventSubtype);
                                return possibleValues;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore, some values can be null along the way
            }
        }
        possibleValues = GetCollisionValueFromGUID(GetGUIDFromCodeName(codeName));
        return possibleValues;
    }
    public List<uint> GetCollisionValueFromGUID(string guid)
    {
        int eventIdx = Convert.ToInt32(EventType.Collision);
        List<uint> possibleValues = new List<uint>();
        for (var i = 0; i < Core.Data.GameObjects.Count; i++)
        {
            UndertaleGameObject obj = Core.Data.GameObjects[i];
            try
            {
                foreach (UndertaleGameObject.Event evnt in obj.Events[eventIdx])
                {
                    foreach (UndertaleGameObject.EventAction action in evnt.Actions)
                    {
                        if (action.CodeId.Name.Content.Contains(guid))
                        {
                            if (!possibleValues.Contains(evnt.EventSubtype))
                            {
                                possibleValues.Add(evnt.EventSubtype);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore, some values can be null along the way
            }
        }

        if (possibleValues.Count == 0)
        {
            possibleValues.Add(uint.MaxValue);
            return possibleValues;
        }
        else
        {
            return possibleValues;
        }
    }
    public string GetGUIDFromCodeName(string codeName)
    {
        string afterPrefix = codeName.Substring(11);
        if (afterPrefix.LastIndexOf("_Collision_") != -1)
        {
            string s2 = "_Collision_";
            return afterPrefix.Substring(afterPrefix.LastIndexOf("_Collision_") + s2.Length, afterPrefix.Length - (afterPrefix.LastIndexOf("_Collision_") + s2.Length));
        }
        else
            return "Invalid";
    }

    public uint ReduceCollisionValue(List<uint> possibleValues)
    {
        if (possibleValues.Count == 1)
        {
            if (possibleValues[0] != uint.MaxValue)
                return possibleValues[0];

            // Nothing found, pick new one
            bool objFound = false;
            uint objIndex = 0;
            while (!objFound)
            {
                string objectIndex = SimpleTextInput("Object could not be found. Please enter it below:",
                    "Object enter box.", "", false).ToLower();
                for (var i = 0; i < Core.Data.GameObjects.Count; i++)
                {
                    if (Core.Data.GameObjects[i].Name.Content.ToLower() == objectIndex)
                    {
                        objFound = true;
                        objIndex = (uint)i;
                    }
                }
            }
            return objIndex;
        }

        if (possibleValues.Count != 0)
        {
            // 2 or more possible values, make a list to choose from

            string gameObjectNames = "";
            foreach (uint objID in possibleValues)
                gameObjectNames += Core.Data.GameObjects[(int)objID].Name.Content + "\n";

            bool objFound = false;
            uint objIndex = 0;
            while (!objFound)
            {
                string objectIndex = SimpleTextInput("Multiple objects were found. Select only one object below from the set, or, if none below match, some other object name:",
                    "Object enter box.", gameObjectNames, true).ToLower();
                for (var i = 0; i < Core.Data.GameObjects.Count; i++)
                {
                    if (Core.Data.GameObjects[i].Name.Content.ToLower() == objectIndex)
                    {
                        objFound = true;
                        objIndex = (uint)i;
                    }
                }
            }
            return objIndex;
        }

        return 0;
    }
}