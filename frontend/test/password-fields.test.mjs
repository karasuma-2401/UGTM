import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import * as React from 'react';
import * as jsxRuntime from 'react/jsx-runtime';
import * as icons from 'lucide-react';

function loadModule(path, dependencies = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  });
  const exports = {};
  new Function('require', 'exports', outputText)(
    (name) => {
      assert.ok(name in dependencies, `Unexpected dependency: ${name}`);
      return { __esModule: true, ...dependencies[name] };
    },
    exports,
  );
  return exports;
}

function collectElements(node, predicate, out = []) {
  if (!React.isValidElement(node)) return out;
  if (predicate(node)) out.push(node);
  for (const child of React.Children.toArray(node.props.children)) {
    collectElements(child, predicate, out);
  }
  return out;
}

function cn(...parts) {
  return parts.filter(Boolean).join(' ');
}

test('Input preserves wrapper DOM structure across error transitions so input never remounts and loses focus', () => {
  const { Input } = loadModule('components/ui/input.tsx', {
    react: {
      ...React,
      useId: () => 'input-desc-id',
      forwardRef: (render) => render,
    },
    'react/jsx-runtime': jsxRuntime,
    '@/lib/utils': { cn },
  });

  const withoutError = Input({ id: 'confirm-password', value: 'A' }, null);
  const withError = Input(
    { id: 'confirm-password', value: 'A', error: 'Mật khẩu xác nhận không khớp' },
    null,
  );

  // Outer wrapper must stay the same <div className="w-full"> in both states
  assert.equal(withoutError.type, 'div');
  assert.equal(withError.type, 'div');
  assert.equal(withoutError.props.className, 'w-full');
  assert.equal(withError.props.className, 'w-full');

  // Inner input wrapper must stay at the exact same child index 0
  const cleanChildren = React.Children.toArray(withoutError.props.children);
  const errorChildren = React.Children.toArray(withError.props.children);
  assert.equal(cleanChildren[0].type, 'div');
  assert.equal(errorChildren[0].type, 'div');
  assert.equal(cleanChildren.length, 1);
  assert.equal(errorChildren.length, 2);
  assert.equal(errorChildren[1].type, 'p');
  assert.equal(errorChildren[1].props.children, 'Mật khẩu xác nhận không khớp');
});

test('PasswordFields only shows mismatch error after confirmation PasswordField loses focus', () => {
  const stateValues = [];
  let cursor = 0;

  const MockPasswordField = () => null;
  const MockPasswordStrength = () => null;

  const { PasswordFields } = loadModule('features/auth/components/PasswordFields.tsx', {
    react: {
      useState(initial) {
        const index = cursor++;
        if (!(index in stateValues)) {
          stateValues[index] = typeof initial === 'function' ? initial() : initial;
        }
        const setState = (next) => {
          stateValues[index] = typeof next === 'function' ? next(stateValues[index]) : next;
        };
        return [stateValues[index], setState];
      },
    },
    'react/jsx-runtime': jsxRuntime,
    './PasswordField': { PasswordField: MockPasswordField },
    './PasswordStrength': { PasswordStrength: MockPasswordStrength },
  });

  let password = 'Password@123';
  let confirmationPassword = '';

  function render(overrides = {}) {
    cursor = 0;
    const tree = PasswordFields({
      password,
      confirmationPassword,
      onPasswordChange: (value) => {
        password = value;
      },
      onConfirmationChange: (value) => {
        confirmationPassword = value;
      },
      autoValidateMismatch: true,
      ...overrides,
    });
    const fields = collectElements(tree, (el) => el.type === MockPasswordField);
    assert.equal(fields.length, 2);
    return { passwordField: fields[0], confirmField: fields[1] };
  }

  // 1. Focus confirmation field and type 1 non-matching character -> no error yet
  let { confirmField } = render();
  confirmField.props.onFocus();
  confirmField.props.onChange('P');
  ({ confirmField } = render());
  assert.equal(confirmationPassword, 'P');
  assert.equal(confirmField.props.error, undefined);

  // 2. Leave focus (blur) while confirmation does not match -> red mismatch error appears
  confirmField.props.onBlur();
  ({ confirmField } = render());
  assert.equal(confirmField.props.error, 'Mật khẩu xác nhận không khớp');

  // 3. Re-focus and continue typing -> error clears while user is editing
  confirmField.props.onFocus();
  ({ confirmField } = render());
  assert.equal(confirmField.props.error, undefined);

  confirmField.props.onChange('Password@123');
  ({ confirmField } = render());
  assert.equal(confirmField.props.error, undefined);

  // 4. Blur after matching password -> remains error-free
  confirmField.props.onBlur();
  ({ confirmField } = render());
  assert.equal(confirmField.props.error, undefined);

  // 5. Explicit confirmationError prop still takes precedence
  ({ confirmField } = render({ confirmationError: 'Lỗi từ server' }));
  assert.equal(confirmField.props.error, 'Lỗi từ server');
});

test('PasswordField eye toggle prevents mousedown default so clicking it does not blur the input', () => {
  const { PasswordField } = loadModule('features/auth/components/PasswordField.tsx', {
    react: {
      default: {
        forwardRef: (render) => render,
      },
      useState: (initial) => [initial, () => {}],
      useId: () => 'pw-id',
    },
    'react/jsx-runtime': jsxRuntime,
    'lucide-react': icons,
    '@/components/ui/input': { Input: () => null },
    '@/lib/utils': { cn },
  });

  const tree = PasswordField({ value: 'secret' }, null);
  const inputNode = collectElements(tree, (el) => typeof el.type === 'function')[0];
  const toggleButton = inputNode.props.rightIcon;

  let prevented = false;
  toggleButton.props.onMouseDown({
    preventDefault() {
      prevented = true;
    },
  });
  assert.equal(prevented, true);
});
