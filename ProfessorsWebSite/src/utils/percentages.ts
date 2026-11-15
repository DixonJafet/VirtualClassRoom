export function validGrade(value: number, maximum: number): boolean {
  return Number.isFinite(value) && Number.isFinite(maximum) &&
    maximum >= 0 && maximum <= 100 && value >= 0 && value <= maximum;
}

export function courseTotal(row: Record<string, string | number>): number {
  const total = Object.entries(row).reduce((sum, [key, value]) => {
    if (key.toLowerCase() === 'name' || key.toLowerCase() === 'total') return sum;
    const number = Number(value);
    return Number.isFinite(number) ? sum + number : sum;
  }, 0);
  return Number(total.toFixed(2));
}
