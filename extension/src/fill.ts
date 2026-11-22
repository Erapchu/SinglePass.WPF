// Runs inside the page (chrome.scripting.executeScript), so it must not use anything from outside its own body.
// Fills only visible, enabled fields of the top-level page, and only if the page is still on the origin whose
// login SinglePass returned (docs/browser-extension.md#extension).

export function fillPage(expectedOrigin: string, login: string, password: string): 'filled' | 'no_fields' | 'page_changed' {
  if (location.origin !== expectedOrigin) return 'page_changed';

  const isUsable = (input: HTMLInputElement): boolean => {
    if (input.disabled || input.readOnly) return false;
    const style = getComputedStyle(input);
    if (style.visibility === 'hidden' || style.display === 'none') return false;
    const box = input.getBoundingClientRect();
    return box.width > 0 && box.height > 0;
  };

  const loginTypes = new Set(['text', 'email', 'tel', '']);
  const inputs = Array.from(document.querySelectorAll('input')).filter(isUsable);
  const passwordField = inputs.find((input) => input.type === 'password');

  let loginField: HTMLInputElement | undefined;
  if (passwordField) {
    // The last login-like field before the password, preferably in the same form.
    const before = inputs.slice(0, inputs.indexOf(passwordField)).filter((input) => loginTypes.has(input.type));
    loginField = before.filter((input) => input.form === passwordField.form).pop() ?? before.pop();
  } else {
    // A first step that asks only for the login: the focused field, or the first login-like one.
    const active = document.activeElement;
    loginField = active instanceof HTMLInputElement && isUsable(active) && loginTypes.has(active.type)
      ? active
      : inputs.find((input) => loginTypes.has(input.type) && /user|login|mail|account|identifier/i.test(
        `${input.name} ${input.id} ${input.autocomplete} ${input.type}`));
  }

  if (!passwordField && !loginField) return 'no_fields';

  // Set through the prototype setter so that frameworks tracking the value (React and others) notice it.
  const setValue = (input: HTMLInputElement, value: string): void => {
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
    input.focus();
    if (setter) setter.call(input, value);
    else input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
  };

  if (loginField && login) setValue(loginField, login);
  if (passwordField) setValue(passwordField, password);
  return 'filled';
}
