import { Form, Radio } from 'antd';

/**
 * An individual's sex, as the LAM FAAS, TD and ownership forms ask for it. Optional personal information (Data Privacy
 * Act): printed only when the office turns Forms:PrintOwnerSex on (docs/analysis/records-and-forms.md Q3).
 */
export function SexItem() {
  return (
    <Form.Item name="sex" label="Sex" extra="Optional. Printed on forms only when the office has enabled it.">
      <Radio.Group
        options={[
          { value: null, label: 'Not stated' },
          { value: 'Female', label: 'Female' },
          { value: 'Male', label: 'Male' },
        ]}
      />
    </Form.Item>
  );
}
