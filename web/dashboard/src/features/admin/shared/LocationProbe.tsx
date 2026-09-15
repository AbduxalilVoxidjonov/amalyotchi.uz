import { useLocation } from 'react-router-dom';

/** Test yordamchisi: joriy `pathname`ni ko'rsatadi — `renderHierarchyPage` navigatsiyani tekshiradi. */
export function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.pathname}</div>;
}
