// Runs inside the page (chrome.scripting.executeScript), so it must not use anything from outside its own body.
// Fills only visible, enabled fields of the top-level page, and only if the page is still on the origin whose
// login Erapkey returned (docs/browser-extension.md#extension).

export type FillOutcome = 'filled' | 'no_fields' | 'no_totp' | 'page_changed';

/** totp: the current one-time code of the entry, or null when the entry has none. */
export function fillPage(expectedOrigin: string, login: string, password: string, totp: string | null): FillOutcome {
  if (location.origin !== expectedOrigin) return 'page_changed';

  const isUsable = (input: HTMLInputElement): boolean => {
    if (input.disabled || input.readOnly) return false;
    const style = getComputedStyle(input);
    if (style.visibility === 'hidden' || style.display === 'none') return false;
    const box = input.getBoundingClientRect();
    return box.width > 0 && box.height > 0;
  };

  const loginTypes = new Set(['text', 'email', 'tel', '']);
  const codeTypes = new Set(['text', 'tel', 'number', 'password', '']);
  const inputs = Array.from(document.querySelectorAll('input')).filter(isUsable);

  // A field for a one-time code (the second step of a sign-in). The login never goes into it: the page would
  // send the login as a wrong code, or show it in plain text.
  const words = (input: HTMLInputElement): Set<string> => new Set(
    [input.name, input.id, input.autocomplete, input.placeholder, input.getAttribute('aria-label') ?? '']
      .join(' ')
      .replace(/(\p{Ll})(\p{Lu})/gu, '$1 $2')
      .toLowerCase()
      .split(/[^\p{L}\p{N}]+/u));
  const codeWords = ['otp', 'totp', 'hotp', '2fa', 'mfa', 'tfa', 'passcode', 'otc', 'код'];
  // Other codes that are not one-time codes: postal, promo, card security codes and the like.
  const otherCodeWords = ['post', 'postal', 'zip', 'promo', 'coupon', 'voucher', 'gift', 'discount', 'referral',
    'invite', 'invitation', 'country', 'area', 'phone', 'captcha', 'card', 'cvv', 'cvc', 'csc', 'промокод', 'индекс'];
  const isCodeField = (input: HTMLInputElement): boolean => {
    if (!codeTypes.has(input.type)) return false;
    const autocomplete = input.autocomplete.toLowerCase();
    if (autocomplete.includes('one-time-code')) return true;
    if (autocomplete.startsWith('cc-')) return false;
    const w = words(input);
    if (otherCodeWords.some((word) => w.has(word))) return false;
    if (codeWords.some((word) => w.has(word))) return true;
    if (w.has('one') && w.has('time')) return true;
    if (w.has('two') && w.has('factor')) return true;
    if (w.has('code') && (w.has('verification') || w.has('verify') || w.has('auth') || w.has('authentication')
      || w.has('security') || w.has('sms') || w.has('login') || w.has('signin') || w.has('confirmation')
      || [input.name, input.id].some((value) => value.toLowerCase() === 'code'))) return true;
    // A short digits-only field: <input inputmode="numeric" maxlength="6">.
    const digitsOnly = input.inputMode === 'numeric' || input.type === 'number' || /^(\\d|\[0-9\])/.test(input.pattern);
    return digitsOnly && input.maxLength >= 4 && input.maxLength <= 8;
  };
  // A code split into boxes of one character each.
  const digitBoxes = inputs.filter((input) => input.maxLength === 1 && codeTypes.has(input.type));
  const codeFields = new Set(digitBoxes.length >= 4 ? digitBoxes : inputs.filter(isCodeField));

  const passwordField = inputs.find((input) => input.type === 'password' && !codeFields.has(input));
  const isLoginField = (input: HTMLInputElement): boolean => loginTypes.has(input.type) && !codeFields.has(input);

  let loginField: HTMLInputElement | undefined;
  if (passwordField) {
    // The last login-like field before the password, preferably in the same form.
    const before = inputs.slice(0, inputs.indexOf(passwordField)).filter(isLoginField);
    loginField = before.filter((input) => input.form === passwordField.form).pop() ?? before.pop();
  } else {
    // A first step that asks only for the login: the focused field, or the first login-like one.
    const active = document.activeElement;
    loginField = active instanceof HTMLInputElement && isUsable(active) && isLoginField(active)
      ? active
      : inputs.find((input) => isLoginField(input) && /user|login|mail|account|identifier/i.test(
        `${input.name} ${input.id} ${input.autocomplete} ${input.type}`));
  }

  const codeField = codeFields.size > 0 ? [...codeFields][0] : undefined;
  if (!passwordField && !loginField && !codeField) return 'no_fields';
  // Only a code is asked for, and the entry has no TOTP.
  if (!passwordField && !loginField && !totp) return 'no_totp';

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
  if (codeField && totp) {
    // Boxes of one digit get a digit each; with fewer boxes than digits the first one gets the whole code,
    // as if it were pasted, and the page spreads it over the boxes itself.
    const boxes = [...codeFields];
    if (boxes.length > 1 && boxes.length >= totp.length) totp.split('').forEach((digit, i) => setValue(boxes[i], digit));
    else setValue(codeField, totp);
  }
  return 'filled';
}
