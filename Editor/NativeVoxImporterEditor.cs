using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using Miventech.NativeVoxReader.VoxRenderer.Types;
using Miventech.NativeVoxReader.Tools;
using Miventech.NativeVoxReader.Data;
using System.Linq;
using UnityEngine;
using Miventech.NativeVoxReader.Editor;

namespace Miventech.NativeVoxReader.Editor
{
    [CustomEditor(typeof(NativeVoxImporter))]
    public class NativeVoxImporterEditor : ScriptedImporterEditor
    {
        private static readonly Color BorderSelected = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color BorderHover = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color BorderNormal = new Color(0f, 0f, 0f, 0.35f);

        private bool _showPalette = false;
        private Color32[] _palette;
        private int _selectedColor = -1;
        private int _hoverColor = -1;
        private string _search = "";
        private float _swatchSize = 26f;
        private Vector2 _paletteScroll;
        private string _statusMessage = "";
        private double _statusUntil = 0;

        public override void OnEnable()
        {
            base.OnEnable();
            var importer = (NativeVoxImporter)target;
            if (importer != null && !string.IsNullOrEmpty(importer.assetPath))
            {
                var loadedVoxFile = new Miventech.NativeVoxReader.Runtime.Tools.ReaderFile.ReaderVoxFile().Read(importer.assetPath);
                if (loadedVoxFile != null && loadedVoxFile.palette != null)
                {
                    _palette = loadedVoxFile.palette.ToColor32Array();
                }
            }
        }

        public override void OnInspectorGUI()
        {
            var importer = (NativeVoxImporter)target;

            var renderTypes = VoxRenderAbstract.GetAllRenderTypes();
            var typeNames = renderTypes.Select(t => t.Name).ToArray();

            int currentIndex = System.Array.IndexOf(typeNames, importer.selectedRenderType);
            if (currentIndex == -1) currentIndex = 0;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Native Vox Importer Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Render Mode", currentIndex, typeNames);
            if (EditorGUI.EndChangeCheck())
            {
                string newTypeName = typeNames[newIndex];

                SerializedProperty typeProp = serializedObject.FindProperty("selectedRenderType");
                typeProp.stringValue = newTypeName;

                var newType = VoxRenderAbstract.GetTypeByName(newTypeName);
                if (newType != null)
                {
                    GameObject temp = new GameObject();
                    temp.hideFlags = HideFlags.HideAndDontSave;
                    var renderer = (VoxRenderAbstract)temp.AddComponent(newType);
                    var settingsType = renderer.SettingsType;
                    Object.DestroyImmediate(temp);

                    SerializedProperty settingsProp = serializedObject.FindProperty("settings");
                    settingsProp.managedReferenceValue = System.Activator.CreateInstance(settingsType);
                }
            }

            EditorGUILayout.Space();

            SerializedProperty sProp = serializedObject.FindProperty("settings");
            if (sProp != null && sProp.managedReferenceValue != null)
            {
                EditorGUILayout.LabelField("Renderer Settings", EditorStyles.boldLabel);
                SerializedProperty iterator = sProp.Copy();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                    enterChildren = false;
                }
            }

            EditorGUILayout.Space();

            SerializedProperty overrideProp = serializedObject.FindProperty("overridePalette");
            SerializedProperty customPaletteProp = serializedObject.FindProperty("customPalette");

            EditorGUILayout.PropertyField(overrideProp);

            if (_palette != null && _palette.Length > 0)
            {
                if (!overrideProp.boolValue) _selectedColor = -1;

                _showPalette = EditorGUILayout.Foldout(_showPalette, $"Palette ({_palette.Length} Colors)", true);
                if (_showPalette)
                {
                    EditorGUI.indentLevel++;

                    EnsureCustomPalette(customPaletteProp, overrideProp.boolValue);

                    // --- Toolbar: búsqueda / tamaño / acciones ---
                    EditorGUILayout.BeginHorizontal();
                    _search = EditorGUILayout.TextField(new GUIContent("Search", "Filter by index or hex (e.g. 12 or FF00AA)"), _search);
                    if (!string.IsNullOrEmpty(_search) && GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        _search = "";
                        GUI.FocusControl(null);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Swatch Size", GUILayout.Width(72));
                    _swatchSize = EditorGUILayout.Slider(_swatchSize, 16f, 48f);
                    using (new EditorGUI.DisabledScope(!overrideProp.boolValue))
                    {
                        if (GUILayout.Button("Reset All", EditorStyles.miniButton, GUILayout.Width(66))) ResetPalette(customPaletteProp);
                    }
                    if (GUILayout.Button("Copy All", EditorStyles.miniButton, GUILayout.Width(66))) CopyAllHex();
                    EditorGUILayout.EndHorizontal();

                    List<int> visible = GetVisibleIndices();

                    if (visible.Count == 0)
                    {
                        EditorGUILayout.HelpBox("No colors match the search filter.", MessageType.Info);
                    }
                    else
                    {
                        DrawSwatchGrid(visible, customPaletteProp, overrideProp.boolValue);
                    }

                    if (overrideProp.boolValue && _selectedColor >= 0 && _selectedColor < _palette.Length)
                    {
                        EditorGUILayout.Space(4);
                        DrawColorDetail(_selectedColor, customPaletteProp);
                    }

                    if (!string.IsNullOrEmpty(_statusMessage))
                    {
                        if (EditorApplication.timeSinceStartup > _statusUntil) _statusMessage = "";
                        else EditorGUILayout.HelpBox(_statusMessage, MessageType.None);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }

        private void EnsureCustomPalette(SerializedProperty customPaletteProp, bool overrideOn)
        {
            if (!overrideOn || customPaletteProp == null) return;
            if (customPaletteProp.arraySize == _palette.Length) return;
            customPaletteProp.arraySize = _palette.Length;
            for (int i = 0; i < _palette.Length; i++) customPaletteProp.GetArrayElementAtIndex(i).colorValue = _palette[i];
        }

        private List<int> GetVisibleIndices()
        {
            var result = new List<int>(_palette.Length);
            string q = string.IsNullOrEmpty(_search) ? null : _search.Trim().TrimStart('#').ToLowerInvariant();
            for (int i = 0; i < _palette.Length; i++)
            {
                Color32 c = _palette[i];
                if (c.a == 0 && c.r == 0 && c.g == 0 && c.b == 0) continue; // Skip empty colors
                if (q != null && !i.ToString().Contains(q) && !ColorUtility.ToHtmlStringRGBA(c).ToLowerInvariant().Contains(q)) continue;
                result.Add(i);
            }
            return result;
        }

        private Color32 GetShownColor(int i, SerializedProperty customPaletteProp, bool overrideOn)
        {
            if (overrideOn && customPaletteProp != null && customPaletteProp.arraySize > i) return customPaletteProp.GetArrayElementAtIndex(i).colorValue;
            return _palette[i];
        }

        private void DrawSwatchGrid(List<int> indices, SerializedProperty customPaletteProp, bool overrideOn)
        {
            float indent = EditorGUI.indentLevel * 15f;
            float cell = _swatchSize + 3f;
            int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - indent - 16f) / cell));
            int rows = Mathf.CeilToInt(indices.Count / (float)columns);
            float contentW = columns * cell + 4f;
            float contentH = rows * cell + 4f;
            float viewH = Mathf.Min(320f, contentH);

            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, GUILayout.Height(viewH));
            Rect grid = GUILayoutUtility.GetRect(contentW, contentH, GUILayout.ExpandWidth(false));

            Event e = Event.current;
            int hover = -1;

            for (int n = 0; n < indices.Count; n++)
            {
                int i = indices[n];
                int col = n % columns;
                int row = n / columns;
                Rect r = new Rect(grid.x + 2f + col * cell, grid.y + 2f + row * cell, _swatchSize, _swatchSize);
                Color32 shown = GetShownColor(i, customPaletteProp, overrideOn);
                string hex = ColorUtility.ToHtmlStringRGBA(shown);

                EditorGUI.DrawRect(r, shown);
                DrawBorder(r, i == _selectedColor ? BorderSelected : (i == _hoverColor ? BorderHover : BorderNormal));
                GUI.Label(r, new GUIContent("", $"#{i}  #{hex}\n{(overrideOn ? "Click: select  •  Right-click: copy hex" : "Click: copy hex")}"), GUIStyle.none);

                if (_swatchSize >= 32f)
                {
                    Rect lr = new Rect(r.x, r.yMax - 14f, r.width, 14f);
                    GUI.Label(lr, i.ToString(), EditorStyles.miniLabel);
                }

                if (r.Contains(e.mousePosition)) hover = i;

                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                {
                    if (e.button == 0)
                    {
                        if (overrideOn) _selectedColor = (_selectedColor == i) ? -1 : i;
                        else CopyHexToClipboard(shown, i);
                        e.Use();
                    }
                    else if (e.button == 1)
                    {
                        CopyHexToClipboard(shown, i);
                        e.Use();
                    }
                }
            }

            if (e.type == EventType.Repaint || e.type == EventType.MouseMove) _hoverColor = hover;

            EditorGUILayout.EndScrollView();
        }

        private void DrawColorDetail(int index, SerializedProperty customPaletteProp)
        {
            if (customPaletteProp == null || customPaletteProp.arraySize <= index) return;
            SerializedProperty el = customPaletteProp.GetArrayElementAtIndex(index);

            EditorGUILayout.LabelField($"Color #{index}", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Color edited = EditorGUILayout.ColorField(new GUIContent("Color", $"Palette slot #{index}"), el.colorValue, true, true, false);
            if (EditorGUI.EndChangeCheck()) el.colorValue = edited;

            EditorGUI.BeginChangeCheck();
            string hex = EditorGUILayout.TextField(new GUIContent("Hex (RGBA)", "Format #RRGGBBAA — press Enter or lose focus to apply"), ColorUtility.ToHtmlStringRGBA(el.colorValue));
            if (EditorGUI.EndChangeCheck() && ColorUtility.TryParseHtmlString(hex, out Color parsed)) el.colorValue = parsed;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Hex", EditorStyles.miniButton)) CopyHexToClipboard(el.colorValue, index);
            if (GUILayout.Button("Revert to Original", EditorStyles.miniButton)) el.colorValue = _palette[index];
            EditorGUILayout.EndHorizontal();
        }

        private void ResetPalette(SerializedProperty customPaletteProp)
        {
            if (customPaletteProp == null) return;
            customPaletteProp.arraySize = _palette.Length;
            for (int i = 0; i < _palette.Length; i++) customPaletteProp.GetArrayElementAtIndex(i).colorValue = _palette[i];
            _selectedColor = -1;
            SetStatus("Palette reset to original values");
        }

        private void CopyHexToClipboard(Color32 color, int index)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            EditorGUIUtility.systemCopyBuffer = "#" + hex;
            SetStatus($"Copied #{hex} (color #{index}) to clipboard");
        }

        private void CopyAllHex()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _palette.Length; i++)
            {
                Color32 c = _palette[i];
                if (c.a == 0 && c.r == 0 && c.g == 0 && c.b == 0) continue;
                sb.AppendLine($"#{i}\t#{ColorUtility.ToHtmlStringRGBA(c)}");
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            SetStatus("Full palette copied to clipboard");
        }

        private void SetStatus(string message)
        {
            _statusMessage = message;
            _statusUntil = EditorApplication.timeSinceStartup + 2.5;
            Repaint();
        }

        private static void DrawBorder(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), color);
        }
    }
}
