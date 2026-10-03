import { useState } from 'react';
import {
  Alert, Button, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tag, Tooltip, Typography, Upload,
} from 'antd';
import { PlusOutlined, UploadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  useCancelMarketRecord, useMarketImport, useMarketTransactions, usePreviewMarketImport, useReviewMarketTransaction, useSaveMarketTransaction,
} from '../../api/marketData';
import {
  useActualUses, useAllMunicipalities, useBarangays, useBuildingTypes, useClassifications, useConveyanceModes, useStructuralTypes,
  useSubClassifications,
} from '../../api/referenceData';
import { ApiRequestError } from '../../lib/apiClient';
import { formatMoney } from '../../lib/format';
import {
  areaMeasureLabel, marketDataSourceLabel, marketReviewColor, type MarketDataReview, type MarketDataSource, type MarketImportResultDto,
  type MarketTransactionDto,
} from '../../lib/marketDataTypes';

const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error).message);
const day = (v?: dayjs.Dayjs | null) => (v ? v.format('YYYY-MM-DD') : null);

/** Sales and other transactions kept as market evidence; each is reviewed before an SMV analysis may use it. */
export function MarketTransactionsTab({ municipalityId }: { municipalityId?: string }) {
  const [page, setPage] = useState(1);
  const [review, setReview] = useState<MarketDataReview>();
  const [search, setSearch] = useState('');
  const { data, isFetching } = useMarketTransactions({ municipalityId, review, search: search || undefined, page, pageSize: 20 });
  const [editing, setEditing] = useState<MarketTransactionDto | 'new' | null>(null);
  const [reviewing, setReviewing] = useState<{ t: MarketTransactionDto; as: 'Accepted' | 'Excluded' } | null>(null);
  const [cancelling, setCancelling] = useState<MarketTransactionDto | null>(null);
  const [importing, setImporting] = useState(false);

  return (
    <>
      <Space wrap style={{ marginBottom: 12, justifyContent: 'space-between', width: '100%' }}>
        <Space wrap>
          <Input.Search allowClear placeholder="Party, document, PIN, TD, lot or title" style={{ width: 300 }} onSearch={(v) => { setSearch(v); setPage(1); }} />
          <Select allowClear placeholder="Review" style={{ width: 160 }} value={review} onChange={(v) => { setReview(v); setPage(1); }}
            options={(['Unreviewed', 'Accepted', 'Excluded'] as MarketDataReview[]).map((r) => ({ value: r, label: r }))} />
        </Space>
        <Space wrap>
          <Button icon={<UploadOutlined />} onClick={() => setImporting(true)}>Import CSV</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New transaction</Button>
        </Space>
      </Space>
      <Table<MarketTransactionDto> rowKey="id" size="small" loading={isFetching} dataSource={data?.items ?? []} scroll={{ x: 'max-content' }}
        pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: 'Date', dataIndex: 'transactionDate' },
          { title: 'Source', dataIndex: 'source', render: (s: MarketDataSource) => marketDataSourceLabel[s] },
          {
            title: 'Location', render: (_, t) => (
              <span>
                {(t.location || t.barangayName) && <>{[t.location, t.barangayName].filter(Boolean).join(', ')}<br /></>}
                <Typography.Text type="secondary">{t.municipalityName}</Typography.Text>
              </span>
            ),
          },
          {
            title: 'Conveyed', render: (_, t) => (
              <span>{[t.conveysLand && 'Land', t.conveysBuilding && 'Building'].filter(Boolean).join(' + ')}
                {t.classificationName && <><br /><Typography.Text type="secondary">{t.classificationName}{t.subClassificationName ? ` · ${t.subClassificationName}` : ''}</Typography.Text></>}
              </span>
            ),
          },
          { title: 'Consideration', dataIndex: 'consideration', align: 'right', render: formatMoney },
          {
            title: 'Unit price', align: 'right', render: (_, t) => (
              <span>
                {t.landUnitPrice !== null && <div>{formatMoney(t.landUnitPrice)} / {areaMeasureLabel[t.landAreaUnit]}</div>}
                {t.buildingUnitPrice !== null && <div>{formatMoney(t.buildingUnitPrice)} / sqm floor</div>}
                {t.landUnitPrice === null && t.buildingUnitPrice === null && <Typography.Text type="secondary">not told apart</Typography.Text>}
              </span>
            ),
          },
          {
            title: 'Review', render: (_, t) => (
              <Tooltip title={t.exclusionReason ?? t.reviewNote}>
                <Tag color={marketReviewColor[t.review]}>{t.review}</Tag>
                {t.importBatch && <Tag>imported</Tag>}
                {t.propertyTransactionId && <Tag>from transfer</Tag>}
              </Tooltip>
            ),
          },
          {
            title: 'Actions', render: (_, t) => (
              <Space size={4} wrap>
                <Button size="small" onClick={() => setEditing(t)}>Edit</Button>
                {t.review !== 'Accepted' && <Button size="small" type="primary" onClick={() => setReviewing({ t, as: 'Accepted' })}>Accept</Button>}
                {t.review !== 'Excluded' && <Button size="small" onClick={() => setReviewing({ t, as: 'Excluded' })}>Exclude</Button>}
                <Button size="small" danger onClick={() => setCancelling(t)}>Cancel</Button>
              </Space>
            ),
          },
        ]} />
      {editing && <TransactionModal value={editing === 'new' ? null : editing} defaultMunicipalityId={municipalityId} onClose={() => setEditing(null)} />}
      {reviewing && <ReviewModal t={reviewing.t} as={reviewing.as} onClose={() => setReviewing(null)} />}
      {cancelling && <CancelModal path="transactions" id={cancelling.id} title="Cancel market transaction" onClose={() => setCancelling(null)} />}
      {importing && <ImportModal onClose={() => setImporting(false)} />}
    </>
  );
}

function TransactionModal({ value, defaultMunicipalityId, onClose }: { value: MarketTransactionDto | null; defaultMunicipalityId?: string; onClose: () => void }) {
  const save = useSaveMarketTransaction();
  const [form] = Form.useForm();
  const municipalityId = Form.useWatch('municipalityId', form) as string | undefined;
  const conveysLand = Form.useWatch('conveysLand', form) as boolean | undefined;
  const conveysBuilding = Form.useWatch('conveysBuilding', form) as boolean | undefined;
  const { data: municipalities = [] } = useAllMunicipalities();
  const { data: barangays = [] } = useBarangays(municipalityId);
  const { data: modes = [] } = useConveyanceModes();
  const { data: classes = [] } = useClassifications();
  const { data: subClasses = [] } = useSubClassifications();
  const { data: uses = [] } = useActualUses();
  const { data: buildingTypes = [] } = useBuildingTypes();
  const { data: structuralTypes = [] } = useStructuralTypes();
  const opts = (rows: { id: string; name: string }[]) => rows.map((r) => ({ value: r.id, label: r.name }));

  return (
    <Modal open title={value ? 'Edit market transaction' : 'New market transaction'} footer={null} width={860} destroyOnHidden onCancel={onClose}>
      {value && value.review !== 'Unreviewed' && (
        <Alert type="warning" showIcon style={{ marginBottom: 12 }} title="Saving a change returns this record to Unreviewed: what was validated is no longer what is recorded." />
      )}
      {save.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not save" description={errorText(save.error)} />}
      <Form form={form} layout="vertical"
        initialValues={value
          ? { ...value, transactionDate: dayjs(value.transactionDate) }
          : { source: 'RegistryAbstract', transactionDate: dayjs(), municipalityId: defaultMunicipalityId, conveysLand: true, conveysBuilding: false, landAreaUnit: 'SquareMetre' }}
        onFinish={(v) => save.mutate({
          id: value?.id, source: v.source, conveyanceModeId: v.conveyanceModeId ?? null, transactionDate: day(v.transactionDate)!,
          documentReference: v.documentReference ?? null, documentFileNumber: v.documentFileNumber ?? null, grantorNames: v.grantorNames ?? null,
          granteeNames: v.granteeNames ?? null, granteeAddress: v.granteeAddress ?? null, municipalityId: v.municipalityId, barangayId: v.barangayId ?? null,
          location: v.location ?? null, propertyId: value?.propertyId ?? null, pin: v.pin ?? null, taxDeclarationNumber: v.taxDeclarationNumber ?? null,
          lotNumber: v.lotNumber ?? null, previousTitleNumber: v.previousTitleNumber ?? null, newTitleNumber: v.newTitleNumber ?? null,
          conveysLand: !!v.conveysLand, conveysBuilding: !!v.conveysBuilding, classificationId: v.classificationId ?? null,
          subClassificationId: v.subClassificationId ?? null, actualUseId: v.actualUseId ?? null, buildingTypeId: v.buildingTypeId ?? null,
          structuralTypeId: v.structuralTypeId ?? null, landArea: v.landArea ?? null, landAreaUnit: v.landAreaUnit, buildingFloorArea: v.buildingFloorArea ?? null,
          consideration: v.consideration, landConsideration: v.conveysLand && v.conveysBuilding ? v.landConsideration ?? null : null, remarks: v.remarks ?? null,
        }, { onSuccess: onClose })}>
        <Space wrap>
          <Form.Item name="source" label="Source" rules={[{ required: true }]}>
            <Select style={{ width: 220 }} options={(Object.keys(marketDataSourceLabel) as MarketDataSource[]).map((s) => ({ value: s, label: marketDataSourceLabel[s] }))} />
          </Form.Item>
          <Form.Item name="conveyanceModeId" label="Mode of conveyance">
            <Select allowClear style={{ width: 200 }} options={opts(modes)} notFoundContent="None configured (content pack)" />
          </Form.Item>
          <Form.Item name="transactionDate" label="Date of transaction" rules={[{ required: true }]}><DatePicker /></Form.Item>
          <Form.Item name="documentReference" label="Document"><Input maxLength={200} style={{ width: 180 }} /></Form.Item>
          <Form.Item name="documentFileNumber" label="File no."><Input maxLength={100} style={{ width: 120 }} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="grantorNames" label="From (grantor)"><Input maxLength={1000} style={{ width: 260 }} /></Form.Item>
          <Form.Item name="granteeNames" label="To (grantee)"><Input maxLength={1000} style={{ width: 260 }} /></Form.Item>
          <Form.Item name="granteeAddress" label="Address of new owner"><Input maxLength={1000} style={{ width: 260 }} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="municipalityId" label="City/municipality" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label" style={{ width: 220 }} onChange={() => form.setFieldValue('barangayId', undefined)}
              options={municipalities.map((m) => ({ value: m.id, label: m.name }))} />
          </Form.Item>
          <Form.Item name="barangayId" label="Barangay">
            <Select allowClear showSearch optionFilterProp="label" style={{ width: 200 }} disabled={!municipalityId} options={opts(barangays)} />
          </Form.Item>
          <Form.Item name="location" label="Street / purok / sitio"><Input maxLength={300} style={{ width: 220 }} /></Form.Item>
        </Space>
        <Space wrap>
          <Form.Item name="pin" label="PIN (as stated)"><Input maxLength={100} style={{ width: 200 }} /></Form.Item>
          <Form.Item name="taxDeclarationNumber" label="TD no."><Input maxLength={100} style={{ width: 160 }} /></Form.Item>
          <Form.Item name="lotNumber" label="Lot no."><Input maxLength={100} style={{ width: 120 }} /></Form.Item>
          <Form.Item name="previousTitleNumber" label="Previous title"><Input maxLength={100} style={{ width: 150 }} /></Form.Item>
          <Form.Item name="newTitleNumber" label="New title"><Input maxLength={100} style={{ width: 150 }} /></Form.Item>
        </Space>
        <Space wrap align="start">
          <Form.Item name="conveysLand" valuePropName="checked" label="Conveyed"><Checkbox>Land</Checkbox></Form.Item>
          <Form.Item name="conveysBuilding" valuePropName="checked" label=" "><Checkbox>Building</Checkbox></Form.Item>
          <Form.Item name="classificationId" label="Classification"><Select allowClear style={{ width: 180 }} options={opts(classes)} /></Form.Item>
          <Form.Item name="subClassificationId" label="Sub-class"><Select allowClear style={{ width: 160 }} options={opts(subClasses)} /></Form.Item>
          <Form.Item name="actualUseId" label="Actual use / crop"><Select allowClear style={{ width: 180 }} options={opts(uses)} /></Form.Item>
        </Space>
        <Space wrap align="start">
          {conveysLand && (
            <>
              <Form.Item name="landArea" label="Land area"><InputNumber<number> min={0} style={{ width: 140 }} /></Form.Item>
              <Form.Item name="landAreaUnit" label="Unit">
                <Select style={{ width: 90 }} options={[{ value: 'SquareMetre', label: 'sqm' }, { value: 'Hectare', label: 'ha' }]} />
              </Form.Item>
            </>
          )}
          {conveysBuilding && (
            <>
              <Form.Item name="buildingFloorArea" label="Floor area (sqm)"><InputNumber<number> min={0} style={{ width: 140 }} /></Form.Item>
              <Form.Item name="buildingTypeId" label="Kind of building"><Select allowClear style={{ width: 180 }} options={opts(buildingTypes)} /></Form.Item>
              <Form.Item name="structuralTypeId" label="Structural type"><Select allowClear style={{ width: 160 }} options={opts(structuralTypes)} /></Form.Item>
            </>
          )}
        </Space>
        <Space wrap align="start">
          <Form.Item name="consideration" label="Consideration" rules={[{ required: true }]}><InputNumber<number> min={0} style={{ width: 200 }} /></Form.Item>
          {conveysLand && conveysBuilding && (
            <Form.Item name="landConsideration" label="Part for the land" extra="Needed to tell the land's and the building's unit prices apart.">
              <InputNumber<number> min={0} style={{ width: 200 }} />
            </Form.Item>
          )}
        </Space>
        <Form.Item name="remarks" label="Remarks"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={save.isPending}>Save</Button>
      </Form>
    </Modal>
  );
}

function ReviewModal({ t, as, onClose }: { t: MarketTransactionDto; as: 'Accepted' | 'Excluded'; onClose: () => void }) {
  const review = useReviewMarketTransaction();
  return (
    <Modal open title={as === 'Accepted' ? 'Accept for the sales analysis' : 'Exclude from the sales analysis'} footer={null} destroyOnHidden onCancel={onClose}>
      <Typography.Paragraph type="secondary">
        {t.transactionDate} · {formatMoney(t.consideration)} · {[t.location, t.barangayName, t.municipalityName].filter(Boolean).join(', ')}
      </Typography.Paragraph>
      {review.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not record the review" description={errorText(review.error)} />}
      <Form layout="vertical" initialValues={{ fieldValidatedOn: t.fieldValidatedOn ? dayjs(t.fieldValidatedOn) : undefined }}
        onFinish={(v) => review.mutate({ id: t.id, review: as, exclusionReason: v.exclusionReason ?? null, fieldValidatedOn: day(v.fieldValidatedOn), note: v.note ?? null },
          { onSuccess: onClose })}>
        {as === 'Excluded' && (
          <Form.Item name="exclusionReason" label="Why it is excluded" rules={[{ required: true }, { max: 500 }]}
            extra="E.g. not at arm's length, related parties, partial interest, forced sale.">
            <Input />
          </Form.Item>
        )}
        <Form.Item name="fieldValidatedOn" label="Validated in the field on"><DatePicker /></Form.Item>
        <Form.Item name="note" label="Note"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={review.isPending}>{as === 'Accepted' ? 'Accept' : 'Exclude'}</Button>
      </Form>
    </Modal>
  );
}

export function CancelModal({ path, id, title, onClose }: { path: 'transactions' | 'building-permits' | 'machinery-registrations'; id: string; title: string; onClose: () => void }) {
  const cancel = useCancelMarketRecord(path);
  const [reason, setReason] = useState('');
  return (
    <Modal open title={title} okText="Cancel record" cancelText="Back" okButtonProps={{ danger: true, disabled: !reason.trim(), loading: cancel.isPending }}
      onCancel={onClose} onOk={() => cancel.mutate({ id, reason: reason.trim() }, { onSuccess: onClose })} destroyOnHidden>
      <Typography.Paragraph type="secondary">The record stays on file, marked cancelled.</Typography.Paragraph>
      {cancel.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Could not cancel" description={errorText(cancel.error)} />}
      <Input.TextArea aria-label="Reason" placeholder="Reason (required)" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
    </Modal>
  );
}

/** Upload → preview with each row's errors → import (refused while any row is invalid; CLAUDE.md §60). */
function ImportModal({ onClose }: { onClose: () => void }) {
  const preview = usePreviewMarketImport();
  const doImport = useMarketImport();
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<MarketImportResultDto | null>(null);
  const imported = doImport.data;
  return (
    <Modal open title="Import market transactions (CSV)" footer={null} width={820} destroyOnHidden onCancel={onClose}>
      <Typography.Paragraph type="secondary">
        Columns (header row): <code>transaction_date</code> (yyyy-MM-dd), <code>municipality</code> (PSGC code or name) and <code>consideration</code> are
        required; optional: source, conveyance_mode, document_reference, document_file_number, grantor, grantee, grantee_address, barangay, location, pin,
        td_number, lot_number, previous_title, new_title, conveys (land, building, both), classification, sub_class, actual_use, building_type,
        structural_type (codes), land_area, land_area_unit (sqm, ha), floor_area, land_consideration, remarks. Imported rows are unreviewed.
      </Typography.Paragraph>
      <Upload accept=".csv,text/csv" maxCount={1} beforeUpload={(f) => { setFile(f); setResult(null); preview.reset(); doImport.reset(); return false; }}
        onRemove={() => { setFile(null); setResult(null); }}>
        <Button icon={<UploadOutlined />}>Choose file</Button>
      </Upload>
      <Space style={{ margin: '12px 0' }}>
        <Button disabled={!file} loading={preview.isPending} onClick={() => file && preview.mutate(file, { onSuccess: setResult })}>Preview</Button>
        <Button type="primary" disabled={!file || !result || result.validCount !== result.rowCount || !!imported} loading={doImport.isPending}
          onClick={() => file && result && doImport.mutate({ file, fingerprint: result.fingerprint })}>
          Import {result ? `${result.rowCount} rows` : ''}
        </Button>
      </Space>
      {preview.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="The file cannot be read" description={errorText(preview.error)} />}
      {doImport.isError && <Alert type="error" showIcon style={{ marginBottom: 12 }} title="Nothing was imported" description={errorText(doImport.error)} />}
      {imported && <Alert type="success" showIcon style={{ marginBottom: 12 }} title={`${imported.importedCount} transactions imported as batch ${imported.batch}`} />}
      {result && (
        <>
          <Alert type={result.validCount === result.rowCount ? 'info' : 'warning'} showIcon style={{ marginBottom: 8 }}
            title={`${result.validCount} of ${result.rowCount} rows are valid${result.validCount === result.rowCount ? '' : ' — correct the file and preview it again'}`} />
          <Table rowKey="line" size="small" dataSource={result.rows} pagination={{ pageSize: 10 }}
            columns={[
              { title: 'Line', dataIndex: 'line', width: 60 },
              { title: 'Valid', dataIndex: 'valid', width: 70, render: (v: boolean) => (v ? <Tag color="green">Yes</Tag> : <Tag color="red">No</Tag>) },
              { title: 'Recorded as / errors', render: (_, r) => (r.valid ? r.summary : r.errors.join(' ')) },
            ]} />
        </>
      )}
    </Modal>
  );
}
