/**
 * Collapses the .NET 10 preview OpenAPI generator's numeric union types before Orval sees
 * them.
 *
 * That generator emits every int/decimal as `type: ["integer", "string"]` with a string
 * `pattern`, advertising that it would *accept* a string on input. Orval faithfully turns
 * that into `number | string`, which is why 93 generated aliases were unusable without a
 * coercion helper at every call site — and why the typed client went unused.
 *
 * Responses are always real JSON numbers (System.Text.Json writes numbers for int and
 * decimal), so the string half is an artifact, not the contract. Collapsing to the numeric
 * type makes the generated model match the wire.
 *
 * Delete this once the preview generator stops emitting the union.
 */
function collapseNumericUnions(node) {
  if (!node || typeof node !== 'object') return node;
  if (Array.isArray(node)) return node.map(collapseNumericUnions);

  if (Array.isArray(node.type) && node.type.includes('string')) {
    const numeric = node.type.find((t) => t === 'integer' || t === 'number');
    if (numeric) {
      // Keep null: a nullable numeric is ["null","integer","string"], and dropping the
      // null half would make an optional field look required. Only the string goes.
      node.type = node.type.includes('null') ? [numeric, 'null'] : numeric;
      // The pattern only described the string half, so it is meaningless now and would
      // otherwise survive as a misleading @pattern annotation on a number.
      delete node.pattern;
    }
  }

  for (const key of Object.keys(node)) collapseNumericUnions(node[key]);
  return node;
}

module.exports = (spec) => collapseNumericUnions(spec);
