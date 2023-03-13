using HeadlessStudio;
using UnityEditor;
using UnityEngine;

namespace HeadlessStudio
{
    /// <summary>
    /// https://github.com/johnearnshaw/unity-inspector-help/
    /// </summary>
    [UnityEditor.CustomPropertyDrawer(typeof(HelpAttribute))]
    public class HelpDrawer : UnityEditor.PropertyDrawer
    {
        // Used for top and bottom padding between the text and the HelpBox border.
        private const int PaddingHeight = 6;

        // Used to add some margin between the the HelpBox and the property.
        private const int MarginHeight = 2;

        //  Global field to store the original (base) property height.
        private float _baseHeight;

        // Custom added height for drawing text area which has the MultilineAttribute.
        private float _addedHeight;

        /// <summary>
        /// A wrapper which returns the PropertyDrawer.attribute field as a HelpAttribute.
        /// </summary>
        private HelpAttribute HelpAttribute => (HelpAttribute) attribute;

        /// <summary>
        /// A helper property to check for RangeAttribute.
        /// </summary>
        private RangeAttribute RangeAttribute
        {
            get
            {
                var attributes = fieldInfo.GetCustomAttributes(typeof(RangeAttribute), true);
                return attributes.Length > 0 ? (RangeAttribute) attributes[0] : null;
            }
        }

        /// <summary>
        /// A helper property to check for MultiLineAttribute.
        /// </summary>
        private MultilineAttribute MultilineAttribute
        {
            get
            {
                var attributes = fieldInfo.GetCustomAttributes(typeof(MultilineAttribute), true);
                return attributes.Length > 0 ? (MultilineAttribute) attributes[0] : null;
            }
        }


        public override float GetPropertyHeight(UnityEditor.SerializedProperty prop, GUIContent label)
        {
            var target = prop.serializedObject.targetObject as HeadlessMovieCapture;
            if (target && !target.showHelp)
            {
                return base.GetPropertyHeight(prop, label);
            }

            // We store the original property height for later use...
            _baseHeight = base.GetPropertyHeight(prop, label);

            // This stops icon shrinking if text content doesn't fill out the container enough.
            float minHeight = PaddingHeight * 5;

            // Calculate the height of the HelpBox using the GUIStyle on the current skin and the inspector
            // window's currentViewWidth.
            var content = new GUIContent(HelpAttribute.text);
            var style = GUI.skin.GetStyle("helpbox");

            var height = style.CalcHeight(content, UnityEditor.EditorGUIUtility.currentViewWidth);

            // We add tiny padding here to make sure the text is not overflowing the HelpBox from the top
            // and bottom.
            height += MarginHeight * 2;

            // Since we draw a custom text area with the label above if our property contains the
            // MultilineAttribute, we need to add some extra height to compensate. This is stored in a
            // seperate global field so we can use it again later.
            if (MultilineAttribute != null && prop.propertyType == SerializedPropertyType.String)
            {
                _addedHeight = 48f;
            }

            // If the calculated HelpBox is less than our minimum height we use this to calculate the returned
            // height instead.
            return height > minHeight ? height + _baseHeight + _addedHeight : minHeight + _baseHeight + _addedHeight;
        }


        public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label)
        {
            var target = prop.serializedObject.targetObject as HeadlessMovieCapture;
            if (target && !target.showHelp)
            {
                EditorGUI.PropertyField(position, prop, label);
                return;
            }

            // We get a local reference to the MultilineAttribute as we use it twice in this method and it
            // saves calling the logic twice for minimal optimization, etc...
            var multiline = MultilineAttribute;

            EditorGUI.BeginProperty(position, label, prop);

            // Copy the position out so we can calculate the position of our HelpBox without affecting the
            // original position.
            var helpPos = position;

            helpPos.height -= _baseHeight + MarginHeight;


            if (multiline != null)
            {
                helpPos.height -= _addedHeight;
            }

            // Renders the HelpBox in the Unity inspector UI.
            EditorGUI.HelpBox(helpPos, HelpAttribute.text, (UnityEditor.MessageType)HelpAttribute.type);

            position.y += helpPos.height + MarginHeight;
            position.height = _baseHeight;


            // If we have a RangeAttribute on our field, we need to handle the PropertyDrawer differently to
            // keep the same style as Unity's default.
            var range = RangeAttribute;

            if (range != null)
            {
                if (prop.propertyType == SerializedPropertyType.Float)
                {
                    EditorGUI.Slider(position, prop, range.min, range.max, label);
                }
                else if (prop.propertyType == SerializedPropertyType.Integer)
                {
                    EditorGUI.IntSlider(position, prop, (int) range.min, (int) range.max, label);
                }
                else
                {
                    // Not numeric so draw standard property field as punishment for adding RangeAttribute to
                    // a property which can not have a range :P
                    EditorGUI.PropertyField(position, prop, label);
                }
            }
            else if (multiline != null)
            {
                // Here's where we handle the PropertyDrawer differently if we have a MultiLineAttribute, to try
                // and keep some kind of multiline text area. This is not identical to Unity's default but is
                // better than nothing...
                if (prop.propertyType == SerializedPropertyType.String)
                {
                    var style = GUI.skin.label;
                    var size = style.CalcHeight(label, EditorGUIUtility.currentViewWidth);

                    EditorGUI.LabelField(position, label);

                    position.y += size;
                    position.height += _addedHeight - size;

                    // Fixed text dissappearing thanks to: http://answers.unity3d.com/questions/244043/textarea-does-not-work-text-dissapears-solution-is.html
                    prop.stringValue = EditorGUI.TextArea(position, prop.stringValue);
                }
                else
                {
                    // Again with a MultilineAttribute on a non-text field deserves for the standard property field
                    // to be drawn as punishment :P
                    EditorGUI.PropertyField(position, prop, label);
                }
            }
            else
            {
                // If we get to here it means we're drawing the default property field below the HelpBox. More custom
                // and built in PropertyDrawers could be implemented to enable HelpBox but it could easily make for
                // hefty else/if block which would need refactoring!
                EditorGUI.PropertyField(position, prop, label);
            }

            EditorGUI.EndProperty();
        }
    }
}