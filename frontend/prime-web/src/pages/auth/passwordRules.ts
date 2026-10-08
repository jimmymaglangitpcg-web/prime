/** Form rule: the confirmation field must equal the form's `password` field. */
export const confirmRule = ({ getFieldValue }: { getFieldValue: (name: string) => unknown }) => ({
  validator: (_: unknown, v: unknown) => (v === getFieldValue('password') ? Promise.resolve() : Promise.reject(new Error('The passwords differ'))),
});
