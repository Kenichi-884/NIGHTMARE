using RoslynCSharp.CodeSecurity;
using System;
using System.Runtime.CompilerServices;
using Trivial.CodeSecurity.Restrictions;
using UnityEditor;
using UnityEngine;

namespace RoslynCSharp
{
    internal sealed class RestrictionsValidatorDrawer
    {
        // Type
        public interface IAssemblyRestrictionsDrawerGUI
        {
            // Methods
            void DrawAssemblyEntry(AssemblyRestriction assembly, Rect entryRect);
            void DrawNamespaceEntry(NamespaceRestriction ns, Rect entryRect);
            void DrawTypeEntry(TypeRestriction type, Rect entryRect);
            bool GetExpandedState(object entry);
            void SetExpandedState(object entry, bool expanded);
        }

        // Private        
        private UnityEngine.Object targetObject = null;
        private GUIStyle rowAStyle = null;
        private GUIStyle rowBStyle = null;
        private bool toggleStyle = true;

        // Public
        public float HierarchyInsetAmount = 16;

        // Properties
        private GUIStyle NextSelectedRowStyle
        {
            get
            {
                // Get style
                GUIStyle style = toggleStyle == true ? rowBStyle : rowAStyle;

                // Flip toggle
                toggleStyle = !toggleStyle;
                return style;
            }
        }

        // Constructor
        public RestrictionsValidatorDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;

            // ### Row A Style
            // Get help box color
            Color helpBoxColor = new Color(0.82f, 0.82f, 0.82f);

            if (EditorGUIUtility.isProSkin == true)
                helpBoxColor = new Color(0.22f, 0.22f, 0.22f);

            rowAStyle = new GUIStyle(EditorStyles.label);
            rowAStyle.stretchWidth = true;
            rowAStyle.normal.background = new Texture2D(1, 1);
            rowAStyle.normal.background.SetPixel(0, 0, helpBoxColor);
            rowAStyle.normal.background.Apply();


            // ### Row B Style
            // Get help box color
            helpBoxColor = new Color(0.79f, 0.79f, 0.79f);

            if (EditorGUIUtility.isProSkin == true)
                helpBoxColor = new Color(0.20f, 0.20f, 0.20f);

            rowBStyle = new GUIStyle(EditorStyles.label);
            rowBStyle.stretchWidth = true;
            rowBStyle.normal.background = new Texture2D(1, 1);
            rowBStyle.normal.background.SetPixel(0, 0, helpBoxColor);
            rowBStyle.normal.background.Apply();           
        }

        // Methods
        #region CalcualteSize
        public float CalculateAssemblyRestrictionTreeViewRequiredHeight(AssemblyRestriction assemblyRestrictions, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer)
        {
            // Check for null
            if (assemblyRestrictions == null)
                return 0;

            // Get line height
            float lineHeight = EditorGUIUtility.singleLineHeight;

            // Calculate total
            return CalculateAssemblyRestrictionTreeViewExpandedCount(assemblyRestrictions, assemblyElementDrawer) * lineHeight;
        }

        public int CalculateAssemblyRestrictionTreeViewExpandedCount(AssemblyRestriction assemblyRestrictions, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer)
        {
            // Check for null
            if (assemblyRestrictions == null)
                return 0;

            // Check for expanded
            if (assemblyElementDrawer.GetExpandedState(assemblyRestrictions) == false)
                return 1;

            // Store total - root is always expanded
            int count = 1;

            // Check all namespaces
            foreach (NamespaceRestriction namespaceRestriction in assemblyRestrictions.NamedTypeRestrictions)
            {
                count += CalculateNamespaceRestrictionTreeViewExpandedCount(namespaceRestriction, assemblyElementDrawer);
            }

            // Check all root types
            foreach (TypeRestriction typeRestriction in assemblyRestrictions.RootTypeRestrictions)
            {
                count += CalculateTypeRestrictionTreeViewExpandedCount(typeRestriction, assemblyElementDrawer);
            }
            return count;
        }

        private int CalculateNamespaceRestrictionTreeViewExpandedCount(NamespaceRestriction namespaceRestrictions, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer)
        {
            // Check for null
            if (namespaceRestrictions == null)
                return 0;

            // Check for expanded
            if (assemblyElementDrawer.GetExpandedState(namespaceRestrictions) == false)
                return 0;

            int count = 0;

            // Check all types
            foreach (TypeRestriction typeRestriction in namespaceRestrictions.TypeRestrictions)
            {
                count += CalculateTypeRestrictionTreeViewExpandedCount(typeRestriction, assemblyElementDrawer);
            }
            return count;
        }

        private int CalculateTypeRestrictionTreeViewExpandedCount(TypeRestriction typeRestrictions, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer)
        {
            // Check for null
            if (typeRestrictions == null)
                return 0;

            return 0;
        }
        #endregion

        public bool DrawAssemblyRestrictionTreeView(CodeSecurityAssemblyRestriction assemblyRestrictions, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer, Rect clipRect, Vector2 scroll)
        {
            // Check for null
            if (assemblyRestrictions == null)
                return false;

            // Reset style toggle
            toggleStyle = true;

            // Dummy label just to generate a rect
            GUILayout.Label("");
            Rect last = clipRect;// GUILayoutUtility.GetLastRect();
            last.height = EditorGUIUtility.singleLineHeight;
            Rect current = last;
            //float startY = current.y -= EditorGUIUtility.singleLineHeight;

            Func<Rect> nextRect = () =>
            {
                current.y += EditorGUIUtility.singleLineHeight;
                return new Rect(current.x, current.y, current.width - 20, current.height);
            };

            // Draw the assembly entry
            bool delete = DrawAssemblyRestrictionEntryTreeView(assemblyRestrictions, assemblyElementDrawer, clipRect, scroll, current, nextRect);

            // Layout space so that following layout controls work correctly
            GUILayout.Space(current.y - EditorGUIUtility.singleLineHeight);

            return delete;
        }

        private bool DrawAssemblyRestrictionEntryTreeView(CodeSecurityAssemblyRestriction assemblyRestrictions, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer, in Rect clipRect, in Vector2 scroll, Rect current, Func<Rect> nextRect)
        {
            bool delete = false;

            // Check for visible
            bool visible = CullingVisibilityCheckFast(clipRect, current, scroll);

            // Check for expanded
            bool expanded = assemblyElementDrawer.GetExpandedState(assemblyRestrictions);

            // Get next rect
            Rect lineRect = current;// nextRect();
            int hierarchyDepth = 0;

            // Display the item
            if (visible == true)
            {
                // Draw background
                GUI.Label(lineRect, GUIContent.none, NextSelectedRowStyle);

                // Update line rect and draw assembly entry
                lineRect.x += HierarchyInsetAmount * hierarchyDepth;
                assemblyElementDrawer.DrawAssemblyEntry(assemblyRestrictions, lineRect);

                // Check for runtime
                if (assemblyRestrictions.Name != "Runtime")
                {
                    // Draw delete button
                    Rect deleteRect = lineRect;
                    deleteRect.x += Screen.width - 52;
                    deleteRect.width = 24;

                    if (GUI.Button(deleteRect, "X" /*EditorGUIUtility.IconContent("d_winbtn_mac_close_h")*/, EditorStyles.label) == true)
                    {
                        delete = true;
                    }
                }
            }

            // Check for expanded - need to draw nested elements
            if (expanded == true)
            {
                if (assemblyRestrictions.Name != "Runtime")
                {
                    // Draw assembly reference asset
                    DrawAssemblyRestrictionReferenceAsset(assemblyRestrictions, nextRect);
                }


                // Draw namespace types
                foreach (NamespaceRestriction ns in assemblyRestrictions.NamedTypeRestrictions)
                    DrawNamespaceRestrictionsEntryTreeView(ns, assemblyElementDrawer, clipRect, scroll, nextRect, hierarchyDepth + 1);

                // Draw root types
                foreach (TypeRestriction type in assemblyRestrictions.RootTypeRestrictions)
                    DrawTypeRestrictionsEntryTreeView(type, assemblyElementDrawer, clipRect, scroll, nextRect, hierarchyDepth + 1);
            }
            return delete;
        }

        private void DrawNamespaceRestrictionsEntryTreeView(NamespaceRestriction namespaceRestrictions, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer, in Rect clipRect, in Vector2 scroll, Func<Rect> nextRect, int hierarchyDepth)
        {
            // Get next rect
            Rect lineRect = nextRect();

            // Check for visible
            bool visible = CullingVisibilityCheckFast(clipRect, lineRect, scroll);

            // Check for expanded
            bool expanded = assemblyElementDrawer.GetExpandedState(namespaceRestrictions);

            // Check for visible
            if (visible == true)
            {
                // Draw background
                GUI.Label(lineRect, GUIContent.none, NextSelectedRowStyle);

                // Update line rect and draw assembly entry
                lineRect.x += HierarchyInsetAmount * hierarchyDepth;
                assemblyElementDrawer.DrawNamespaceEntry(namespaceRestrictions, lineRect);
            }

            // Check for expanded
            if (expanded == true)
            {
                foreach (TypeRestriction type in namespaceRestrictions.TypeRestrictions)
                    DrawTypeRestrictionsEntryTreeView(type, assemblyElementDrawer, clipRect, scroll, nextRect, hierarchyDepth + 1);
            }
        }

        private void DrawTypeRestrictionsEntryTreeView(TypeRestriction typeRestrictions, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer, in Rect clipRect, in Vector2 scroll, Func<Rect> nextRect, int hierarchyDepth)
        {
            // Get next rect
            Rect lineRect = nextRect();

            // Check for visible
            bool visible = CullingVisibilityCheckFast(clipRect, lineRect, scroll);

            // Check for expanded
            bool expanded = assemblyElementDrawer.GetExpandedState(typeRestrictions);

            // Check for visible
            if (visible == true)
            {
                // Draw background
                GUI.Label(lineRect, GUIContent.none, NextSelectedRowStyle);

                // Update line rect and draw assembly entry
                lineRect.x += HierarchyInsetAmount * hierarchyDepth;
                assemblyElementDrawer.DrawTypeEntry(typeRestrictions, lineRect);
            }

            //// Check for expanded
            //if (expanded == true)
            //{
            //    foreach (MemberRestriction member in typeRestrictions.MemberRestrictions)
            //        DrawMemberRestrictionEntryTreeView(member, assemblyElementDrawer, clipRect, scroll, nextRect, hierarchyDepth + 1);
            //}
        }

        //private void DrawMemberRestrictionEntryTreeView(MemberRestriction memberRestriction, IAssemblyRestrictionsDrawerGUI assemblyElementDrawer, in Rect clipRect, in Vector2 scroll, Func<Rect> nextRect, int hierarchyDepth)
        //{
        //    // Get next rect
        //    Rect lineRect = nextRect();

        //    // Check for visible
        //    bool visible = CullingVisibilityCheckFast(clipRect, lineRect, scroll);

        //    // Check for visible
        //    if (visible == true)
        //    {
        //        // Draw background
        //        GUI.Label(lineRect, GUIContent.none, NextSelectedRowStyle);

        //        // Update line rect and draw assembly entry
        //        lineRect.x += HierarchyInsetAmount * hierarchyDepth;
        //        assemblyElementDrawer.DrawMemberEntry(memberRestriction, lineRect);
        //    }
        //}

        private void DrawAssemblyRestrictionReferenceAsset(CodeSecurityAssemblyRestriction assemblyRestriction, Func<Rect> nextRect)
        {
            // Display assembly reference asset
            Rect area = nextRect();

            // Draw background
            GUI.Label(area, GUIContent.none, NextSelectedRowStyle);

            area.x += 45;
            area.width -= 45;


            GUI.Label(area, EditorGUIUtility.IconContent("d_Linked"));
            area.x += 27;
            area.width -= 27;

            Rect labelArea = new Rect(area.x, area.y, EditorGUIUtility.labelWidth, area.height);
            GUI.Label(labelArea, new GUIContent("Reference Assembly", "The assembly reference asset used to auto-update the security restrictions included for this assembly. Useful if the assembly is in constant development as changes will be updated automatically keeping your original restrictions where possible"));

            Rect fieldArea = new Rect(labelArea.xMax - 20, area.y, area.width - EditorGUIUtility.labelWidth, area.height);
            AssemblyReferenceAsset modifiedReferenceAsset = EditorGUI.ObjectField(fieldArea, assemblyRestriction.ReferenceAssemblyAsset, typeof(AssemblyReferenceAsset), false) as AssemblyReferenceAsset;

            // Check for changed
            if (modifiedReferenceAsset != assemblyRestriction.ReferenceAssemblyAsset)
            {
                // Apply update
                assemblyRestriction.ReferenceAssemblyAsset = modifiedReferenceAsset;

                // Apply changes
                EditorUtility.SetDirty(targetObject);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CullingVisibilityCheckFast(in Rect clipRect, in Rect elementRect, in Vector2 scroll)
        {
            return true;
            //if (elementRect.y < clipRect.y + scroll.y - elementRect.height
            //        || elementRect.yMax > clipRect.yMax + scroll.y)
            //{
            //    // Element is not visible and should not be rendered to save overhead
            //    return false;
            //}

            //// Element is visible and should be drawn
            //return true;
        }
    }
}
