import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import ts from 'typescript';
import { deploymentEnv, routes } from '@vercel/config/v1';

// Vercel Git checks config before executing the module. Keep the exported
// object readable as JSON literals, including destinations and env bindings.
function readLiteral(node) {
  if (ts.isStringLiteral(node) || ts.isNumericLiteral(node)) {
    return ts.isNumericLiteral(node) ? Number(node.text) : node.text;
  }
  if (node.kind === ts.SyntaxKind.TrueKeyword) return true;
  if (node.kind === ts.SyntaxKind.FalseKeyword) return false;
  if (ts.isArrayLiteralExpression(node)) return node.elements.map(readLiteral);
  if (ts.isObjectLiteralExpression(node)) {
    return Object.fromEntries(
      node.properties.map((property) => {
        assert.ok(ts.isPropertyAssignment(property), 'Config must use literal properties');
        assert.ok(
          ts.isIdentifier(property.name) || ts.isStringLiteral(property.name),
          'Config must use static property names',
        );
        return [property.name.text, readLiteral(property.initializer)];
      }),
    );
  }
  assert.fail(`Vercel Git cannot statically read ${ts.SyntaxKind[node.kind]}`);
}

test('Vercel Git can read routes without evaluating environment expressions', () => {
  const file = new URL('../vercel.ts', import.meta.url);
  const source = ts.createSourceFile(
    'vercel.ts',
    readFileSync(file, 'utf8'),
    ts.ScriptTarget.Latest,
  );
  const declaration = source.statements
    .filter(ts.isVariableStatement)
    .flatMap((statement) => statement.declarationList.declarations)
    .find((item) => ts.isIdentifier(item.name) && item.name.text === 'config');
  assert.ok(declaration?.initializer, 'Missing exported Vercel config');
  const config = readLiteral(declaration.initializer);
  assert.deepEqual(config.rewrites, [
    routes.rewrite('/api/(.*)', `${deploymentEnv('API_ORIGIN')}/api/$1`),
    routes.rewrite('/(.*)', '/index.html'),
  ]);
  assert.equal(config.git.deploymentEnabled, false);
});
