/**
 * A barcode in the one form the server keeps it in (`Domain/Products/Gtin.cs`, which has the reasoning): digits only
 * with a correct check digit; a UPC-A is padded with a zero to 13 and a UPC-E expanded to its UPC-A first, so the
 * symbology a phone read does not choose the household's row. Null when the digits are no barcode, so nothing wrong
 * is sent and a typing slip is told on the spot.
 */
export function readGtin(text: string): string | null {
  // People copy "8 076800 195057" from a pack, spaces and all.
  const digits = text.replace(/[\s-]/g, '');

  if (!/^\d+$/.test(digits)) {
    return null;
  }

  switch (digits.length) {
    case 8:
      return eight(digits);
    case 12:
      return hasCheckDigit(digits) ? `0${digits}` : null;
    case 13:
    case 14:
      return hasCheckDigit(digits) ? digits : null;
    default:
      return null;
  }
}

/** The digits weigh 3, 1, 3, 1 from the right, the check digit last and weighing 1; a valid code sums to a multiple of ten. */
function hasCheckDigit(digits: string): boolean {
  let sum = 0;

  for (let index = 0; index < digits.length; index += 1) {
    sum += Number(digits[index]) * ((digits.length - 1 - index) % 2 === 0 ? 1 : 3);
  }

  return sum % 10 === 0;
}

/** An EAN-8 whenever its check digit fits one; otherwise a UPC-E, whose check digit is the UPC-A's it stands for. */
function eight(digits: string): string | null {
  if (hasCheckDigit(digits)) {
    return digits;
  }

  if (digits[0] !== '0' && digits[0] !== '1') {
    return null;
  }

  const expanded = expandUpcE(digits);

  return hasCheckDigit(expanded) ? `0${expanded}` : null;
}

/** Number system, six digits, check digit: where the six go in the twelve depends on the last of them. */
function expandUpcE(upcE: string): string {
  const system = upcE[0]!;
  const d = upcE.slice(1, 7);
  const check = upcE[7]!;

  switch (d[5]) {
    case '0':
    case '1':
    case '2':
      return `${system}${d[0]}${d[1]}${d[5]}0000${d[2]}${d[3]}${d[4]}${check}`;
    case '3':
      return `${system}${d[0]}${d[1]}${d[2]}00000${d[3]}${d[4]}${check}`;
    case '4':
      return `${system}${d[0]}${d[1]}${d[2]}${d[3]}00000${d[4]}${check}`;
    default:
      return `${system}${d.slice(0, 5)}0000${d[5]}${check}`;
  }
}
