using System.Collections.Generic;
using Trivial.CodeSecurity.Restrictions;
using UnityEditor;
using UnityEngine;

namespace RoslynCSharp
{
    internal class RestrictionsValidatorElementDrawer : RestrictionsValidatorDrawer.IAssemblyRestrictionsDrawerGUI
    {
        // Private
        private UnityEngine.Object targetObject = null;
        private Dictionary<object, bool> expandedLookup = new Dictionary<object, bool>();

        // Public
        public float LabelOffsetFromToggle = 40;

        // Constructor
        public RestrictionsValidatorElementDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;
        }

        // Methods
        public bool GetExpandedState(object entry)
        {
            // Check for expanded
            bool expanded;
            if (expandedLookup.TryGetValue(entry, out expanded) == true)
                return expanded;

            // Add default
            expandedLookup[entry] = false;
            return false;
        }

        public void SetExpandedState(object entry, bool expanded)
        {
            expandedLookup[entry] = expanded;
        }

        public virtual void DrawAssemblyEntry(AssemblyRestriction assembly, Rect entryRect)
        {            
            // Get mixed
            RestrictionSelection selection = assembly.GetAllowedNamespacesAndTypesCombined(out _, out _);

            // Check for on
            bool on = selection == RestrictionSelection.Allow;

            // Draw assembly entry
            bool onResult = DrawEntry(assembly, assembly.Name, entryRect, on, selection == RestrictionSelection.Mixed, assembly.HasTypes == false);

            // Check for changed
            if (onResult != on)
            {
                Undo.RecordObject(targetObject, "Modify code security restrictions");

                assembly.SetAllowedNamespacesAndTypesCombined(onResult == true ? RestrictionAllow.Allow : RestrictionAllow.Deny);
                EditorUtility.SetDirty(targetObject);
            }
        }

        public virtual void DrawNamespaceEntry(NamespaceRestriction ns, Rect entryRect)
        {
            // Get mixed
            RestrictionSelection selection = ns.GetAllowedTypesCombined(out _, out _);

            // Check for on
            bool on = selection == RestrictionSelection.Allow;

            // Draw namespace entry
            bool onResult = DrawEntry(ns, ns.Name, entryRect, on, selection == RestrictionSelection.Mixed, ns.HasTypes == false);

            // Check for changed
            if (onResult != on)
            {
                Undo.RecordObject(targetObject, "Modify code security restrictions");

                ns.SetAllowedTypesCombined(onResult == true ? RestrictionAllow.Allow : RestrictionAllow.Deny);
                EditorUtility.SetDirty(targetObject);
            }
        }

        public virtual void DrawTypeEntry(TypeRestriction type, Rect entryRect)
        {
            // Check for on
            bool on = type.IsAllowed;

            // Draw type entry
            bool onResult = DrawEntry(type, type.Name, entryRect, on, false, true);

            // Check for changed
            if (onResult != on)
            {
                Undo.RecordObject(targetObject, "Modify code security restrictions");

                type.Allowed = onResult == true ? RestrictionAllow.Allow : RestrictionAllow.Deny;
                EditorUtility.SetDirty(targetObject);
            }
        }

        //public virtual void DrawMemberEntry(CodeMemberRestriction member, Rect entryRect)
        //{
        //    // Check for on
        //    bool on = member.MemberAllowed == CodeRestrictionAllowed.Allow;

        //    // Draw entry
        //    bool onResult = DrawEntry(member, member.MemberName, entryRect, on, false, true);

        //    // Check for changed
        //    if (onResult != on)
        //    {
        //        member.MemberAllowed = onResult == true ? CodeRestrictionAllowed.Allow : CodeRestrictionAllowed.Deny;
        //        EditorUtility.SetDirty(targetObject);
        //    }
        //}

        protected virtual bool DrawEntry(object entry, string name, Rect lineRect, bool on, bool mixed, bool leafNode)
        {
            lineRect.x += 16;
            lineRect.width -= 16;

            Rect foldoutRect = new Rect(lineRect.x, lineRect.y, 16, lineRect.height);
            Rect toggleRect = new Rect(lineRect.x + 16, lineRect.y, 16, lineRect.height);
            Rect labelRect = new Rect(lineRect.x + LabelOffsetFromToggle, lineRect.y, lineRect.width - LabelOffsetFromToggle, lineRect.height);

            // Check for foldout
            if (leafNode == false)
            {
                // Get expanded
                bool expanded = GetExpandedState(entry);

                // Draw foldout
                bool expandedResult = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none);

                // Check for changed
                if (expandedResult != expanded)
                {
                    SetExpandedState(entry, expandedResult);
                }
            }


            // Draw toggle
            EditorGUI.showMixedValue = mixed;
            bool onResult = EditorGUI.Toggle(toggleRect, on);
            EditorGUI.showMixedValue = false;


            // Display entry name
            GUI.Label(labelRect, name);

            return onResult;// || mixed;
        }
    }
}
