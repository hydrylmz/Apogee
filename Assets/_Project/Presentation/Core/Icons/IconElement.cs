using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Icons
{
    /// <summary>
    /// VisualElement that renders a vector icon from <see cref="IconLibrary"/> via
    /// <c>generateVisualContent</c>. Single-color by design: the icon is tinted by the
    /// element's resolved <c>color</c> style, so USS classes can recolor it freely.
    /// </summary>
    public sealed class IconElement : VisualElement
    {
        public const string UssClassName = "apogee-icon";
        public const float DefaultSize = 24f;

        private IconType _icon = IconType.None;

        /// <summary>Which icon to draw. Changing this triggers a repaint.</summary>
        public IconType Icon
        {
            get => _icon;
            set
            {
                if (_icon == value)
                {
                    return;
                }

                _icon = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Square edge length in pixels. Changing this relayouts and repaints.</summary>
        public float IconSize
        {
            get => resolvedStyle.width > 0f ? resolvedStyle.width : style.width.value.value;
            set
            {
                style.width = value;
                style.height = value;
                MarkDirtyRepaint();
            }
        }

        public IconElement(IconType icon = IconType.None, float size = DefaultSize)
        {
            AddToClassList(UssClassName);
            generateVisualContent += OnGenerateVisualContent;

            style.width = size;
            style.height = size;
            _icon = icon;
        }

        /// <summary>Convenience factory matching the "icon in a row" idiom.</summary>
        public static IconElement Create(IconType icon, float size = DefaultSize, string additionalClass = null)
        {
            var element = new IconElement(icon, size);
            if (!string.IsNullOrEmpty(additionalClass))
            {
                element.AddToClassList(additionalClass);
            }

            return element;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_icon == IconType.None)
            {
                return;
            }

            Rect rect = contentRect;
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            Color tint = resolvedStyle.color;
            if (tint.a < 0.01f)
            {
                tint = Color.white; // No explicit color style anywhere in the hierarchy.
            }

            IconLibrary.Draw(ctx.painter2D, _icon, rect, tint);
        }
    }
}
