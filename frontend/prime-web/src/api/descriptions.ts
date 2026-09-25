import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPost, apiPut } from '../lib/apiClient';
import type {
  AddBuildingFloorRequest, AddBuildingMaterialRequest, BuildingDto, MachineryDto, PropertyProfileDto, PropertyTransactionDto,
  SetTransferTaxClearanceRequest, UpdateBuildingDescriptionRequest, UpdateMachineryDescriptionRequest, UpdatePropertyDescriptionRequest,
} from '../lib/types';

/**
 * Descriptive fields printed on the FAAS and TD (docs/analysis/mrpaao-forms-model.md
 * §10): corrections carry a reason, kept in the audit log.
 */
function useInvalidating<TReq, TRes>(fn: (r: TReq) => Promise<TRes>, keys: unknown[][]) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => keys.forEach((queryKey) => queryClient.invalidateQueries({ queryKey })),
  });
}

export const useUpdatePropertyDescription = (propertyId: string) =>
  useInvalidating((r: UpdatePropertyDescriptionRequest) => apiPut<PropertyProfileDto>(`/api/properties/${propertyId}/description`, r),
    [['properties', propertyId]]);

export const useUpdateBuildingDescription = (buildingId: string, rpuId: string) =>
  useInvalidating((r: UpdateBuildingDescriptionRequest) => apiPut<BuildingDto>(`/api/buildings/${buildingId}/description`, r),
    [['rpus', rpuId, 'building']]);

export const useAddBuildingFloor = (buildingId: string, rpuId: string) =>
  useInvalidating((r: AddBuildingFloorRequest) => apiPost<BuildingDto>(`/api/buildings/${buildingId}/floors`, r), [['rpus', rpuId, 'building']]);

export const useAddBuildingMaterial = (buildingId: string, rpuId: string) =>
  useInvalidating((r: AddBuildingMaterialRequest) => apiPost<BuildingDto>(`/api/buildings/${buildingId}/materials`, r), [['rpus', rpuId, 'building']]);

export const useUpdateMachineryDescription = (rpuId: string) =>
  useInvalidating((r: UpdateMachineryDescriptionRequest & { id: string }) => apiPut<MachineryDto>(`/api/machinery/${r.id}/description`, r),
    [['rpus', rpuId, 'machinery']]);

export const useSetTaxClearance = (propertyId: string, transactionId: string) =>
  useInvalidating((r: SetTransferTaxClearanceRequest) => apiPut<PropertyTransactionDto>(`/api/transactions/${transactionId}/tax-clearance`, r),
    [['properties', propertyId, 'transactions']]);
