using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AleksaRistic
{
    class StazioneDiServizio
    {
        private int capacitaSerbatoio;
        private int carburanteDisponibile;
        private int numeroPompe;
        private SemaphoreSlim pompe;
        private Queue<(int idAuto, int richiesta)> codaAuto = new Queue<(int, int)>();
        private object lockObject = new object();  //per fare in modo che possa controllare un solo thread , per esempio quando il carburante alle pompe è minore della richiesta
        private bool rifornimentoInCorso = false;
        private Random random = new Random();
        private int pompaAttuale = 1;

        // Contatore per il numero di auto da servire
        private int autoDaServire;

        public StazioneDiServizio(int numeroPompe, int capacitaSerbatoio, int autoDaServire)
        {
            this.numeroPompe = numeroPompe;
            this.capacitaSerbatoio = capacitaSerbatoio;
            this.carburanteDisponibile = capacitaSerbatoio; // Il serbatoio inizia pieno
            this.autoDaServire = autoDaServire;
            pompe = new SemaphoreSlim(numeroPompe, numeroPompe);
        }

        public async Task RifornisciAuto(int idAuto, int carburanteRichiesto)
        {
            int pompaUsata;
            await pompe.WaitAsync(); // Attende una pompa libera

            lock (lockObject)
            {
                pompaUsata = pompaAttuale;
                pompaAttuale = (pompaAttuale % numeroPompe) + 1;
                Console.WriteLine($"Auto {idAuto} sta entrando nella pompa {pompaUsata}...");

                if (carburanteDisponibile < carburanteRichiesto)
                {
                    Console.WriteLine($"Auto {idAuto} non trova abbastanza carburante e si mette in coda per il rifornimento.");
                    codaAuto.Enqueue((idAuto, carburanteRichiesto));
                    // Non decrementiamo autoDaServire perché l'auto non è stata servita
                    pompe.Release(); // Libera la pompa
                    return;
                }

                // Se c'è carburante sufficiente, esegue il rifornimento
                carburanteDisponibile -= carburanteRichiesto;
                Console.WriteLine($"Auto {idAuto} ha fatto rifornimento di {carburanteRichiesto} litri. Carburante rimanente: {carburanteDisponibile}");
                autoDaServire--; // L'auto è servita
            }

            // Simula il tempo impiegato per il rifornimento
            await Task.Delay(1000);
            Console.WriteLine($"Auto {idAuto} ha terminato il rifornimento e lascia la pompa {pompaUsata}.");
            pompe.Release();
        }

        public async Task RiempireSerbatoio()
        {
            while (true)
            {
                // Controlla se tutte le auto sono state servite e se le pompe sono libere
                lock (lockObject)
                {
                    if (autoDaServire <= 0 && pompe.CurrentCount == numeroPompe)
                    {
                        Console.WriteLine("Tutte le auto sono state rifornite. L'autobotte termina il servizio.");
                        break;
                    }
                }

                // Simula l'arrivo randomico dell'autobotte
                await Task.Delay(random.Next(5000, 15000));

                lock (lockObject)
                {
                    // Se non tutte le pompe sono libere, l'autobotte aspetta
                    if (pompe.CurrentCount != numeroPompe)
                    {
                        Console.WriteLine("Autobotte in attesa che tutte le pompe siano libere...");
                        continue;
                    }
                    // Se il serbatoio è già pieno, non è necessario rifornire
                    if (carburanteDisponibile == capacitaSerbatoio)
                    {
                        Console.WriteLine("Il serbatoio è già pieno. Autobotte attende...");
                        continue;
                    }

                    Console.WriteLine("Autobotte sta rifornendo il serbatoio...");
                    rifornimentoInCorso = true;
                }

                // Simula il tempo di rifornimento del serbatoio
                await Task.Delay(2000);

                lock (lockObject)
                {
                    carburanteDisponibile = capacitaSerbatoio;
                    rifornimentoInCorso = false;
                    Console.WriteLine("Serbatoio riempito alla capacità massima!");
                    // Se ci sono auto in coda, notifichiamo la possibilità di riprovare
                    Monitor.PulseAll(lockObject);
                }

                // Gestione della coda: riprocessa le auto in coda se il carburante è sufficiente
                lock (lockObject)
                {
                    while (codaAuto.Count > 0 && carburanteDisponibile >= codaAuto.Peek().richiesta)
                    {
                        var (idAuto, richiesta) = codaAuto.Dequeue();
                        // Avvia la procedura di rifornimento per l'auto in coda
                        Task.Run(async () => await RifornisciAuto(idAuto, richiesta));
                    }
                }
            }
        }
    }

    class Programma
    {
        static async Task Main()
        {
            int numeroPompe = 3;
            int capacitaSerbatoio = 50;
            int numeroAuto = 7;
            StazioneDiServizio stazione = new StazioneDiServizio(numeroPompe, capacitaSerbatoio, numeroAuto);

            List<Task> autoTasks = new List<Task>();

            // Avvio delle auto con richieste casuali
            for (int i = 1; i <= numeroAuto; i++)
            {
                int idAuto = i;
                int richiesta = new Random().Next(5, 15);
                autoTasks.Add(Task.Run(async () => await stazione.RifornisciAuto(idAuto, richiesta)));
            }

            // Avvio dell'autobotte
            Task autobotteTask = Task.Run(async () => await stazione.RiempireSerbatoio());

            // Attende il completamento di tutte le auto
            await Task.WhenAll(autoTasks);
            Console.WriteLine("Tutte le auto hanno terminato il rifornimento. In attesa che l'autobotte termini...");

            // Attende che anche l'autobotte termini (dopo che tutte le auto sono servite)
            await autobotteTask;
        }
    }
}
