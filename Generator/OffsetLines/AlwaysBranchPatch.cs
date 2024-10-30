using Il2CppDumper;
using Gee.External.Capstone;
using Keystone;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gee.External.Capstone.Arm;
using Gee.External.Capstone.Arm64;

namespace Generator.OffsetLines
{
    class AlwaysBranchPatch : PatchLine
    {
        public Line CalledMethod { get; set; }

        public override void FindPatch(ScriptJson scriptJson, Stream il2cpp, Architecture architecture)
        {
            base.FindPatch(scriptJson, il2cpp, architecture);
            CalledMethod.FindOffset(scriptJson);

            if (startOffset != 0)
            {
                ulong count = endOffset == 0 ? (ulong)il2cpp.Length - startOffset : endOffset - startOffset;
                il2cpp.Position = (long)startOffset;

                const byte bufferSize = 4;
                byte[] buffer = new byte[bufferSize];
                ulong readed = 0;

                Mode mode;
                switch (architecture)
                {
                    case Architecture.ARM:
                        mode = Mode.ARM;
                        break;
                    case Architecture.ARM64:
                        mode = Mode.LITTLE_ENDIAN;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                using (Engine keystone = new Engine(architecture, mode) { ThrowOnError = true })
                {
                    switch (architecture)
                    {
                        case Architecture.ARM:
                            // Ваш код для ARM архитектуры
                            break;
                        case Architecture.ARM64:
                            using (var disassembler = CapstoneDisassembler.CreateArm64Disassembler(Arm64DisassembleMode.LittleEndian))
                            {
                                disassembler.EnableInstructionDetails = true;
                                do
                                {
                                    var pos = il2cpp.Position;
                                    readed += (ulong)il2cpp.Read(buffer, 0, bufferSize);
                                    var instruction = disassembler.Disassemble(buffer, pos).First();

                                    Console.WriteLine($"Disassembled instruction at position {pos}: {instruction.Mnemonic} {instruction.Operand}");

                                    if (instruction.Id == Arm64InstructionId.ARM64_INS_BL)
                                    {
                                        var newPos = instruction.Details.Operands.First().Immediate;
                                        Console.WriteLine($"Instruction is BL, Operand Immediate is {newPos}");

                                        if (newPos == (long)CalledMethod.Offset)
                                        {
                                            Offset = (ulong)il2cpp.Position;
                                            il2cpp.Read(buffer, 0, bufferSize);
                                            instruction = disassembler.Disassemble(buffer, (long)Offset).First();

                                            Console.WriteLine($"Next instruction after BL points to CalledMethod.Offset, instruction: {instruction.Mnemonic} {instruction.Operand}");
                                            Console.WriteLine($"Attempting to assemble branch instruction at Offset {Offset}");

                                            int idx = instruction.Operand.LastIndexOf('#');
                                            if (idx >= 0)
                                            {
                                                var operandSuffix = instruction.Operand.Substring(idx);
                                                Console.WriteLine($"Operand suffix: {operandSuffix}");
                                                PatchData = keystone.Assemble($"b {operandSuffix}", Offset).Buffer;
                                            }
                                            else
                                            {
                                                Console.WriteLine("Error: '#' not found in instruction operand.");
                                                Console.WriteLine($"Instruction Operand: {instruction.Operand}");
                                                throw new Exception("Cannot find '#' in instruction operand.");
                                            }
                                            break;
                                        }
                                        pos = il2cpp.Position;
                                        il2cpp.Position = newPos;
                                        il2cpp.Read(buffer, 0, bufferSize);
                                        instruction = disassembler.Disassemble(buffer, newPos).First();

                                        Console.WriteLine($"Moved to new position {newPos}, instruction: {instruction.Mnemonic} {instruction.Operand}");

                                        if (instruction.Id == Arm64InstructionId.ARM64_INS_B && instruction.Details.Operands.First().Immediate == (long)CalledMethod.Offset)
                                        {
                                            il2cpp.Position = pos;
                                            Offset = (ulong)pos;
                                            il2cpp.Read(buffer, 0, bufferSize);
                                            instruction = disassembler.Disassemble(buffer, (long)Offset).First();

                                            Console.WriteLine($"Instruction is B pointing to CalledMethod.Offset, instruction: {instruction.Mnemonic} {instruction.Operand}");
                                            Console.WriteLine($"Attempting to assemble branch instruction at Offset {Offset}");

                                            int idx = instruction.Operand.LastIndexOf('#');
                                            if (idx >= 0)
                                            {
                                                var operandSuffix = instruction.Operand.Substring(idx);
                                                Console.WriteLine($"Operand suffix: {operandSuffix}");
                                                PatchData = keystone.Assemble($"b {operandSuffix}", Offset).Buffer;
                                            }
                                            else
                                            {
                                                Console.WriteLine("Error: '#' not found in instruction operand.");
                                                Console.WriteLine($"Instruction Operand: {instruction.Operand}");
                                                throw new Exception("Cannot find '#' in instruction operand.");
                                            }
                                            break;
                                        }
                                        il2cpp.Position = pos;
                                    }
                                }
                                while (readed < count);
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }
        }
    }
}
