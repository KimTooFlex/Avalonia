using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Input.Navigation;
using Avalonia.VisualTree;

namespace Avalonia.Input
{
    /// <summary>
    /// Handles keyboard navigation for a window.
    /// </summary>
    public class KeyboardNavigationHandler : IKeyboardNavigationHandler
    {
        /// <summary>
        /// The window to which the handler belongs.
        /// </summary>
        private IInputRoot? _owner;

        /// <summary>
        /// Sets the owner of the keyboard navigation handler.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <remarks>
        /// This method can only be called once, typically by the owner itself on creation.
        /// </remarks>
        public void SetOwner(IInputRoot owner)
        {
            if (_owner != null)
            {
                throw new InvalidOperationException("AccessKeyHandler owner has already been set.");
            }

            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown);
        }

        /// <summary>
        /// Gets the next control in the specified navigation direction.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="direction">The navigation direction.</param>
        /// <returns>
        /// The next element in the specified direction, or null if <paramref name="element"/>
        /// was the last in the requested direction.
        /// </returns>
        public static IInputElement? GetNext(
            IInputElement element,
            NavigationDirection direction)
        {
            element = element ?? throw new ArgumentNullException(nameof(element));

            // If there's a custom keyboard navigation handler as an ancestor, use that.
            var custom = (element as Visual)?.FindAncestorOfType<ICustomKeyboardNavigation>(true);
            if (custom is object && HandlePreCustomNavigation(custom, element, direction, out var ce))
                return ce;

            var result = direction switch
            {
                NavigationDirection.Next => TabNavigation.GetNextTab(element, false),
                NavigationDirection.Previous => TabNavigation.GetPrevTab(element, null, false),
                _ => throw new NotSupportedException(),
            };

            // If there wasn't a custom navigation handler as an ancestor of the current element,
            // but there is one as an ancestor of the new element, use that.
            if (custom is null && HandlePostCustomNavigation(element, result, direction, out ce))
                return ce;

            return result;
        }

        /// <summary>
        /// Moves the focus in the specified direction.
        /// </summary>
        /// <param name="element">The current element.</param>
        /// <param name="direction">The direction to move.</param>
        /// <param name="keyModifiers">Any key modifiers active at the time of focus.</param>
        public void Move(
            IInputElement element,
            NavigationDirection direction,
            KeyModifiers keyModifiers = KeyModifiers.None)
        {
            element = element ?? throw new ArgumentNullException(nameof(element));

            var next = GetNext(element, direction);

            if (next != null)
            {
                var method = direction == NavigationDirection.Next ||
                             direction == NavigationDirection.Previous ?
                             NavigationMethod.Tab : NavigationMethod.Directional;
                FocusManager.Instance?.Focus(next, method, keyModifiers);
            }
        }

        /// <summary>
        /// Handles the Tab key being pressed in the window.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        protected virtual void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var current = FocusManager.Instance?.Current;

            if (current != null && e.Key == Key.Tab)
            {
                var direction = (e.KeyModifiers & KeyModifiers.Shift) == 0 ?
                    NavigationDirection.Next : NavigationDirection.Previous;
                Move(current, direction, e.KeyModifiers);
                e.Handled = true;
            }
        }

        private static bool HandlePreCustomNavigation(
            ICustomKeyboardNavigation customHandler,
            IInputElement element,
            NavigationDirection direction,
            [NotNullWhen(true)] out IInputElement? result)
        {
            if (customHandler != null)
            {
                var (handled, next) = customHandler.GetNext(element, direction);

                if (handled)
                {
                    if (next != null)
                    {
                        result = next;
                        return true;
                    }
                    else if (direction == NavigationDirection.Next || direction == NavigationDirection.Previous)
                    {
                        var r = direction switch
                        {
                            NavigationDirection.Next => TabNavigation.GetNextTabOutside(customHandler),
                            NavigationDirection.Previous => TabNavigation.GetPrevTabOutside(customHandler),
                            _ => throw new NotSupportedException(),
                        };

                        if (r is object)
                        {
                            result = r;
                            return true;
                        }
                    }
                }
            }

            result = null;
            return false;
        }

        private static bool HandlePostCustomNavigation(
            IInputElement element,
            IInputElement? newElement,
            NavigationDirection direction,
            [NotNullWhen(true)] out IInputElement? result)
        {
            if (newElement is Visual v)
            {
                var customHandler = v.FindAncestorOfType<ICustomKeyboardNavigation>(true);

                if (customHandler is object)
                {
                    var (handled, next) = customHandler.GetNext(element, direction);

                    if (handled && next is object)
                    {
                        result = next;
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }
    }
}
