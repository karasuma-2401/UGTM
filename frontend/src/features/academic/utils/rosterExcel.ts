import type { UserSummary } from '@/features/identity/types/user.types';

export interface RawParsedRow {
  rowNumber: number;
  email: string;
  fullName: string;
  userCode: string;
  phoneNumber?: string;
}

export type RosterValidationStatus = 'Valid' | 'NeedsFix' | 'DuplicateCode';

export interface ValidatedRosterRow {
  rowNumber: number;
  userCode: string;
  fullName: string;
  email: string;
  phoneNumber?: string;
  status: RosterValidationStatus;
  statusLabel: string;
  note: string;
}

export async function parseXlsxFile(file: File): Promise<RawParsedRow[]> {
  const arrayBuffer = await file.arrayBuffer();
  const bytes = new Uint8Array(arrayBuffer);
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const files: Record<string, string> = {};
  let offset = 0;

  while (offset + 30 <= bytes.length) {
    const sig = view.getUint32(offset, true);
    if (sig !== 0x04034b50) break;

    const compression = view.getUint16(offset + 8, true);
    const compressedSize = view.getUint32(offset + 18, true);
    const nameLen = view.getUint16(offset + 26, true);
    const extraLen = view.getUint16(offset + 28, true);

    const nameBytes = bytes.subarray(offset + 30, offset + 30 + nameLen);
    const fileName = new TextDecoder().decode(nameBytes);

    const dataStart = offset + 30 + nameLen + extraLen;
    const dataEnd = dataStart + compressedSize;
    const fileData = bytes.subarray(dataStart, dataEnd);

    if (compression === 0) {
      files[fileName] = new TextDecoder().decode(fileData);
    } else if (compression === 8 && typeof DecompressionStream !== 'undefined') {
      try {
        const ds = new DecompressionStream('deflate-raw');
        const writer = ds.writable.getWriter();
        writer.write(fileData);
        writer.close();
        const reader = ds.readable.getReader();
        const chunks: Uint8Array[] = [];
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          if (value) chunks.push(value);
        }
        const totalLen = chunks.reduce((acc, c) => acc + c.length, 0);
        const decompressed = new Uint8Array(totalLen);
        let cur = 0;
        for (const c of chunks) {
          decompressed.set(c, cur);
          cur += c.length;
        }
        files[fileName] = new TextDecoder().decode(decompressed);
      } catch {}
    }

    offset = dataEnd;
  }

  const sharedStringsXml = files['xl/sharedStrings.xml'] || files['xl/SharedStrings.xml'];
  const sharedStrings: string[] = [];
  if (sharedStringsXml) {
    const siRegex = /<si\b[^>]*>([\s\S]*?)<\/si>/gi;
    let siMatch: RegExpExecArray | null;
    while ((siMatch = siRegex.exec(sharedStringsXml)) !== null) {
      const inner = siMatch[1];
      const tRegex = /<t\b[^>]*>([\s\S]*?)<\/t>/gi;
      let tMatch: RegExpExecArray | null;
      let str = '';
      while ((tMatch = tRegex.exec(inner)) !== null) {
        str += tMatch[1];
      }
      sharedStrings.push(str);
    }
  }

  const sheetXml = files['xl/worksheets/sheet1.xml'] || files['xl/worksheets/Sheet1.xml'];
  if (!sheetXml) return [];

  const rawRows: Array<{ rowNumber: number; cells: Record<string, string> }> = [];
  const rowRegex = /<row\b[^>]*?\br="(\d+)"[^>]*>([\s\S]*?)<\/row>/gi;
  let rowMatch: RegExpExecArray | null;

  while ((rowMatch = rowRegex.exec(sheetXml)) !== null) {
    const rowNumber = parseInt(rowMatch[1], 10);
    const rowContent = rowMatch[2];
    const cellRegex = /<c\b[^>]*?\br="([A-Z]+)\d+"(?:[^>]*?\bt="([a-z]+)")?[^>]*>([\s\S]*?)<\/c>/gi;
    let cellMatch: RegExpExecArray | null;
    const cells: Record<string, string> = {};

    while ((cellMatch = cellRegex.exec(rowContent)) !== null) {
      const col = cellMatch[1];
      const type = cellMatch[2];
      const cellInner = cellMatch[3];

      let val = '';
      if (type === 's') {
        const vMatch = /<v>(\d+)<\/v>/i.exec(cellInner);
        if (vMatch) {
          const idx = parseInt(vMatch[1], 10);
          val = sharedStrings[idx] || '';
        }
      } else if (type === 'inlineStr') {
        const tMatch = /<t\b[^>]*>([\s\S]*?)<\/t>/i.exec(cellInner);
        if (tMatch) val = tMatch[1];
      } else {
        const vMatch = /<v>([\s\S]*?)<\/v>/i.exec(cellInner);
        if (vMatch) val = vMatch[1];
      }
      cells[col] = val.trim();
    }

    rawRows.push({ rowNumber, cells });
  }

  if (rawRows.length === 0) return [];

  const headerRow = rawRows[0];
  const colMap: Record<string, 'email' | 'fullName' | 'userCode' | 'phoneNumber'> = {};

  for (const [col, val] of Object.entries(headerRow.cells)) {
    const normalized = val.toLowerCase().replace(/[\s_-]/g, '');
    if (normalized === 'email') colMap[col] = 'email';
    else if (['fullname', 'hoten', 'hovaten', 'ten'].includes(normalized)) colMap[col] = 'fullName';
    else if (['usercode', 'mssv', 'masv', 'masinhvien', 'magiangvien', 'macanbo', 'manguoidung'].includes(normalized))
      colMap[col] = 'userCode';
    else if (['phonenumber', 'sodienthoai', 'dienthoai', 'sdt'].includes(normalized)) colMap[col] = 'phoneNumber';
  }

  const result: RawParsedRow[] = [];

  for (let i = 1; i < rawRows.length; i++) {
    const { rowNumber, cells } = rawRows[i];
    let email = '';
    let fullName = '';
    let userCode = '';
    let phoneNumber = '';

    for (const [col, val] of Object.entries(cells)) {
      const field = colMap[col];
      if (field === 'email') email = val;
      else if (field === 'fullName') fullName = val;
      else if (field === 'userCode') userCode = val;
      else if (field === 'phoneNumber') phoneNumber = val;
    }

    if (!email && !fullName && !userCode) {
      if (cells['A']) email = cells['A'];
      if (cells['B']) fullName = cells['B'];
      if (cells['C']) userCode = cells['C'];
      if (cells['D']) phoneNumber = cells['D'];
    }

    if (email || fullName || userCode) {
      result.push({
        rowNumber,
        email,
        fullName,
        userCode,
        phoneNumber,
      });
    }
  }

  return result;
}

export function validateRosterRows(
  rows: RawParsedRow[],
  role: 'Student' | 'Lecturer',
  existingUsers: UserSummary[],
): ValidatedRosterRow[] {
  const existingEmailMap = new Map<string, UserSummary>();
  const existingCodeMap = new Map<string, UserSummary>();

  for (const user of existingUsers) {
    if (user.email) existingEmailMap.set(user.email.toLowerCase().trim(), user);
    if (user.userCode) existingCodeMap.set(user.userCode.toUpperCase().trim(), user);
  }

  const seenCodes = new Map<string, number>();

  return rows.map((row) => {
    const emailLower = row.email.toLowerCase().trim();
    const codeUpper = row.userCode.toUpperCase().trim();

    if (!row.email || !row.fullName) {
      return {
        rowNumber: row.rowNumber,
        userCode: row.userCode || '—',
        fullName: row.fullName || '—',
        email: row.email || '—',
        phoneNumber: row.phoneNumber,
        status: 'NeedsFix',
        statusLabel: 'Cần sửa',
        note: 'Thiếu họ tên hoặc email',
      };
    }

    if (role === 'Student' && !codeUpper) {
      return {
        rowNumber: row.rowNumber,
        userCode: '—',
        fullName: row.fullName,
        email: row.email,
        phoneNumber: row.phoneNumber,
        status: 'NeedsFix',
        statusLabel: 'Cần sửa',
        note: 'Thiếu MSSV bắt buộc',
      };
    }

    if (role === 'Student' && !emailLower.endsWith('@gm.uit.edu.vn') && !emailLower.endsWith('@uit.edu.vn')) {
      return {
        rowNumber: row.rowNumber,
        userCode: row.userCode,
        fullName: row.fullName,
        email: row.email,
        phoneNumber: row.phoneNumber,
        status: 'NeedsFix',
        statusLabel: 'Cần sửa',
        note: 'Email chưa khớp email trường',
      };
    }

    if (codeUpper) {
      const prevRow = seenCodes.get(codeUpper);
      if (prevRow !== undefined) {
        return {
          rowNumber: row.rowNumber,
          userCode: row.userCode,
          fullName: row.fullName,
          email: row.email,
          phoneNumber: row.phoneNumber,
          status: 'DuplicateCode',
          statusLabel: 'Trùng mã',
          note: `Trùng ${role === 'Student' ? 'MSSV' : 'mã cán bộ'} với dòng ${prevRow}`,
        };
      }
      seenCodes.set(codeUpper, row.rowNumber);
    }

    const existingByEmail = existingEmailMap.get(emailLower);
    const existingByCode = codeUpper ? existingCodeMap.get(codeUpper) : undefined;
    const existing = existingByEmail || existingByCode;

    if (existing) {
      if (existing.status === 'PendingActivation') {
        return {
          rowNumber: row.rowNumber,
          userCode: row.userCode,
          fullName: row.fullName,
          email: row.email,
          phoneNumber: row.phoneNumber,
          status: 'Valid',
          statusLabel: 'Hợp lệ',
          note: 'Đã có - chờ kích hoạt',
        };
      }

      return {
        rowNumber: row.rowNumber,
        userCode: row.userCode,
        fullName: row.fullName,
        email: row.email,
        phoneNumber: row.phoneNumber,
        status: 'Valid',
        statusLabel: 'Hợp lệ',
        note: 'Đã có - giữ tài khoản hiện tại',
      };
    }

    return {
      rowNumber: row.rowNumber,
      userCode: row.userCode,
      fullName: row.fullName,
      email: row.email,
      phoneNumber: row.phoneNumber,
      status: 'Valid',
      statusLabel: 'Hợp lệ',
      note: 'Sẵn sàng nhập',
    };
  });
}

